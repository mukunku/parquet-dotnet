using System;
using System.IO;
using Parquet.Encodings;
using Parquet.Meta;
using Xunit;

namespace Parquet.Test;

public class ParquetEncoderTest {

    [Fact]
    public void DecodeOutOfRangeSpan() {
        var source = new MemoryStream(new byte[] {
            64,  66,  15,  0, 0, 0, 0, 0,
            128, 195, 201, 1, 0, 0, 0, 0,
            1,   1,   1,   1, 1, 1, 1, 1, 1 });

        var decoded = new TimeSpan[2];
        ParquetPlainEncoder.Decode(source.ToArray().AsSpan(), decoded.AsSpan(), new SchemaElement { Type = Meta.Type.INT64 });

        Assert.Equal(new TimeSpan(0, 0, 1), decoded[0]);
        Assert.Equal(new TimeSpan(0, 0, 30), decoded[1]);

#if NET6_0_OR_GREATER
        var decodedAsTimeOnly = new TimeOnly[2];
        ParquetPlainEncoder.Decode(source.ToArray().AsSpan(), decodedAsTimeOnly.AsSpan(), new SchemaElement { Type = Meta.Type.INT64 });
        Assert.Equal(new TimeOnly(0, 0, 1), decodedAsTimeOnly[0]);
        Assert.Equal(new TimeOnly(0, 0, 30), decodedAsTimeOnly[1]);
#endif
    }

    [Fact]
    public void DecodeOversizedSpan() {
        var source = new MemoryStream(new byte[] {
            1, 0, 0, 0,
            2, 0, 0, 0,
            0 //<- bad byte of trailing data in source
        });

        //We should still be able to read the data and ignore the extra byte
        int[] decoded = new int[2];
        ParquetPlainEncoder.Decode(source.ToArray().AsSpan(), decoded.AsSpan());

        Assert.Equal(1, decoded[0]);
        Assert.Equal(2, decoded[1]);
    }

    [Fact]
    public void Decode_ByteArrays_WithEmptyArrays() {
        var source = new MemoryStream();

        source.Write(BitConverter.GetBytes(3), 0, 4);
        source.Write([1, 2, 3], 0, 3);
        source.Write(BitConverter.GetBytes(0), 0, 4);
        source.Write(BitConverter.GetBytes(2), 0, 4);
        source.Write([4, 5], 0, 2);

        byte[][] decoded = new byte[3][];
        int elementsRead = ParquetPlainEncoder.Decode(
            source.ToArray().AsSpan(),
            decoded.AsSpan(),
            new SchemaElement { Type = Meta.Type.BYTE_ARRAY });

        Assert.Equal(3, elementsRead);
        Assert.Equal(new byte[] { 1, 2, 3 }, decoded[0]);
        Assert.NotNull(decoded[1]);
        Assert.Empty(decoded[1]);
        Assert.Equal(new byte[] { 4, 5 }, decoded[2]);
    }

    // ---- page data shorter than the page header says: clear ParquetException instead of a bare
    // ArgumentOutOfRangeException / IndexOutOfRangeException from deep inside the decoder

    private static byte[] LengthPrefixed(params byte[][] values) {
        var ms = new MemoryStream();
        foreach(byte[] v in values) {
            ms.Write(BitConverter.GetBytes(v.Length));
            ms.Write(v);
        }
        return ms.ToArray();
    }

    private static SchemaElement ByteArrayColumn(string name) => new() { Name = name, Type = Meta.Type.BYTE_ARRAY };

    [Fact]
    public void Decode_ByteArray_FewerValuesThanExpected_Throws() {
        byte[] source = LengthPrefixed([1, 2], [3]);
        var decoded = new ReadOnlyMemory<byte>[3];

        ParquetException ex = Assert.Throws<ParquetException>(
            () => ParquetPlainEncoder.Decode(source.AsSpan(), decoded.AsSpan(), ByteArrayColumn("blob")));
        Assert.Equal("column 'blob': page data ends after 2 of 3 values, file is corrupt", ex.Message);
    }

    [Fact]
    public void Decode_ByteArray_ValueLongerThanPage_Throws() {
        byte[] source = [.. BitConverter.GetBytes(10), 1, 2, 3];
        var decoded = new byte[1][];

        ParquetException ex = Assert.Throws<ParquetException>(
            () => ParquetPlainEncoder.Decode(source.AsSpan(), decoded.AsSpan(), ByteArrayColumn("blob")));
        Assert.Equal("column 'blob': page data ends after 0 of 1 values, file is corrupt", ex.Message);
    }

    [Fact]
    public void Decode_ByteArray_NegativeLength_Throws() {
        byte[] source = [.. BitConverter.GetBytes(-5), 1, 2, 3];
        var decoded = new ReadOnlyMemory<byte>[1];

        ParquetException ex = Assert.Throws<ParquetException>(
            () => ParquetPlainEncoder.Decode(source.AsSpan(), decoded.AsSpan(), ByteArrayColumn("blob")));
        Assert.Contains("invalid length -5", ex.Message);
    }

    [Fact]
    public void Decode_FixedLenByteArray_FewerValuesThanExpected_Throws() {
        byte[] source = [1, 2, 3, 4, 5, 6];    // one and a half 4-byte values
        var decoded = new ReadOnlyMemory<byte>[2];
        var tse = new SchemaElement { Name = "fixed", Type = Meta.Type.FIXED_LEN_BYTE_ARRAY, TypeLength = 4 };

        ParquetException ex = Assert.Throws<ParquetException>(
            () => ParquetPlainEncoder.Decode(source.AsSpan(), decoded.AsSpan(), tse));
        Assert.Equal("column 'fixed': page data ends after 1 of 2 values, file is corrupt", ex.Message);
    }

    [Fact]
    public void Decode_String_ValueCutOff_Throws() {
        byte[] full = LengthPrefixed("abc"u8.ToArray(), "defgh"u8.ToArray());
        byte[] source = full[..^2];         // second string loses its last two bytes
        var decoded = new ReadOnlyMemory<char>[2];

        ParquetException ex = Assert.Throws<ParquetException>(
            () => ParquetPlainEncoder.Decode(source.AsSpan(), decoded.AsSpan(), ByteArrayColumn("name")));
        Assert.Equal("column 'name': page data ends after 1 of 2 values, file is corrupt", ex.Message);
    }

    [Fact]
    public void Decode_ByteArrayDecimal_EmptyValue_Throws() {
        // 1.23, <empty>, -1.23 as big endian two's complement unscaled values
        byte[] source = LengthPrefixed([0x00, 0x7B], [], [0xFF, 0x85]);
        var decoded = new decimal[3];
        var tse = new SchemaElement { Name = "price", Type = Meta.Type.BYTE_ARRAY, Precision = 5, Scale = 2 };

        ParquetException ex = Assert.Throws<ParquetException>(
            () => ParquetPlainEncoder.Decode(source.AsSpan(), decoded.AsSpan(), tse));
        Assert.Equal("column 'price': value 2 is an empty byte array, which is not a valid DECIMAL, file is corrupt", ex.Message);
    }

    [Fact]
    public void Decode_ByteArrayDecimal_Valid() {
        byte[] source = LengthPrefixed([0x00, 0x7B], [0xFF, 0x85]);
        var decoded = new decimal[2];
        var tse = new SchemaElement { Name = "price", Type = Meta.Type.BYTE_ARRAY, Precision = 5, Scale = 2 };

        Assert.Equal(2, ParquetPlainEncoder.Decode(source.AsSpan(), decoded.AsSpan(), tse));
        Assert.Equal(new[] { 1.23m, -1.23m }, decoded);
    }
}