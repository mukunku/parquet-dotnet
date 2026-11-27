using System;
using System.Collections.Generic;
using System.IO;
using Parquet.Extensions;

namespace Parquet.Meta.Proto;

class ThriftCompactProtocolReader {
    private readonly Stream _inputStream;
    private readonly Stack<short> _lastField = new Stack<short>(15);
    private short _lastFieldId;

    public ThriftCompactProtocolReader(Stream inputStream) {
        _inputStream = inputStream;
    }

    public void StructBegin() {
        _lastField.Push(_lastFieldId);
        _lastFieldId = 0;
    }

    public void StructEnd() {
        _lastFieldId = _lastField.Pop();
    }

    private static int ZigzagToInt(uint n) {
        return (int)(n >> 1) ^ -(int)(n & 1);
    }

    private static long ZigzagToLong(ulong n) {
        return (long)(n >> 1) ^ -(long)(n & 1);
    }

    /// <summary>
    /// Reads one byte, throwing at the end of the stream. Stream.ReadByte returns -1 there, which as a byte is 0xFF:
    /// a variable-length-int continuation byte forever, so truncated metadata used to make varint reads loop endlessly.
    /// </summary>
    private byte ReadRawByte() {
        int b = _inputStream.ReadByte();
        if(b < 0)
            throw new ThriftProtocolException("Unexpected end of Thrift data. File is most likely truncated or corrupt.");
        return (byte)b;
    }

    private uint ReadVarInt32() {
        /*
        Read an i32 from the wire as a varint. The MSB of each byte is set
        if there is another byte to follow. This can Read up to 5 bytes.
        */

        uint result = 0;
        int shift = 0;

        while(true) {
            byte b = ReadRawByte();
            result |= (uint)(b & 0x7f) << shift;
            if((b & 0x80) != 0x80) {
                break;
            }
            shift += 7;
        }

        return result;
    }

    private ulong ReadVarInt64() {
        /*
        Read an i64 from the wire as a proper varint. The MSB of each byte is set
        if there is another byte to follow. This can Read up to 10 bytes.
        */

        int shift = 0;
        ulong result = 0;
        while(true) {
            byte b = ReadRawByte();
            result |= (ulong)(b & 0x7f) << shift;
            if((b & 0x80) != 0x80) {
                break;
            }
            shift += 7;
        }

        return result;
    }

    public bool ReadBool() {
        byte b = ReadRawByte();
        return (CompactType)b == CompactType.BooleanTrue;
    }

    public sbyte ReadByte() {
        return (sbyte)ReadRawByte();
    }

    public short ReadI16() {
        return (short)ZigzagToInt(ReadVarInt32());
    }

    public int ReadI32() {
        return ZigzagToInt(ReadVarInt32());
    }

    public long ReadI64() {
        return ZigzagToLong(ReadVarInt64());
    }

    public double ReadDouble() {
        byte[] data = _inputStream.ReadBytesExactly(sizeof(double));
        if(!BitConverter.IsLittleEndian) {
            Array.Reverse(data);
        }

        return BitConverter.ToDouble(data, 0);
    }

    public byte[] ReadBinary() {
        // read length
        int length = (int)ReadVarInt32();
        if(length == 0) {
            return Array.Empty<byte>();
        }

        // read data
        return _inputStream.ReadBytesExactly(length);
    }

    public string ReadString() {
        // read length
        int length = (int)ReadVarInt32();
        if(length == 0) {
            return string.Empty;
        }

        // read data
        byte[] utf8Data = _inputStream.ReadBytesExactly(length);
        return System.Text.Encoding.UTF8.GetString(utf8Data, 0, length);
    }

    public bool ReadNextField(out short fieldId, out CompactType compactType) {

        // Read a field header off the wire.
        byte header = ReadRawByte();

        // if it's a stop, then we can return immediately, as the struct is over.
        if((CompactType)header == CompactType.Stop) {
            fieldId = 0;
            compactType = 0;
            return false;
        }

        // mask off the 4 MSB of the exType header. it could contain a field id delta.
        short modifier = (short)((header & 0xf0) >> 4);
        compactType = (CompactType)(byte)(header & 0x0f);

        if(modifier == 0) {
            fieldId = ReadI16();
        } else {
            fieldId = (short)(_lastFieldId + modifier);
        }

        // push the new field onto the field stack so we can keep the deltas going.
        _lastFieldId = fieldId;
        return true;
    }

    public int ReadListHeader(out CompactType elementType) {
        /*
        Read a list header off the wire. If the list size is 0-14, the size will
        be packed into the element exType header. If it's a longer list, the 4 MSB
        of the element exType header will be 0xF, and a varint will follow with the
        true size.
        */

        byte sizeAndType = ReadRawByte();
        int size = (sizeAndType >> 4) & 0x0f;
        if(size == 15) {
            size = (int)ReadVarInt32();
        }
        elementType = (CompactType)(byte)(sizeAndType & 0x0f);
        return size;
    }

    /// <summary>
    /// Skips the value of a field that is not known to the reader, e.g. one added by a newer version of the format.
    /// </summary>
    public void SkipField(CompactType compactType) {
        switch(compactType) {
            case CompactType.BooleanTrue:
            case CompactType.BooleanFalse:
                // a bool field's value is its type in the field header, there is nothing more to skip
                break;
            case CompactType.Byte:
                ReadRawByte();
                break;
            case CompactType.I16:
                ReadI16();
                break;
            case CompactType.I32:
                ReadI32();
                break;
            case CompactType.I64:
                ReadI64();
                break;
            case CompactType.Double:
                ReadDouble();
                break;
            case CompactType.Binary:
                // Don't try to decode the string, just skip it.
                ReadBinary();
                break;
            case CompactType.Uuid:
                _inputStream.ReadBytesExactly(16);
                break;
            case CompactType.Struct:
                StructBegin();
                while(ReadNextField(out _, out CompactType fieldType)) {
                    SkipField(fieldType);
                }
                StructEnd();
                break;
            case CompactType.List:
            case CompactType.Set: {   // a set is encoded exactly like a list
                    int elementCount = ReadListHeader(out CompactType elementType);
                    for(int i = 0; i < elementCount; i++) {
                        SkipElement(elementType);
                    }
                }
                break;
            case CompactType.Map: {
                    // size as varint, then (when not empty) one byte with the key type and value type, then the pairs
                    int pairCount = (int)ReadVarInt32();
                    if(pairCount > 0) {
                        byte types = ReadRawByte();
                        var keyType = (CompactType)(byte)(types >> 4);
                        var valueType = (CompactType)(byte)(types & 0x0f);
                        for(int i = 0; i < pairCount; i++) {
                            SkipElement(keyType);
                            SkipElement(valueType);
                        }
                    }
                }
                break;
            default:
                throw new ThriftProtocolException($"Invalid data in Thrift header. File is most likely corrupt (unexpected data type {compactType}).");
        }
    }

    /// <summary>
    /// Skips one element of a list, set or map. Elements are encoded like field values, except bools: a bool element
    /// takes a byte of its own, as there is no field header to carry its value.
    /// </summary>
    private void SkipElement(CompactType elementType) {
        if((elementType == CompactType.BooleanTrue) || (elementType == CompactType.BooleanFalse))
            ReadRawByte();
        else
            SkipField(elementType);
    }
}
