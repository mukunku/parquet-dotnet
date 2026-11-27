using System;
using System.Buffers;
using System.Buffers.Binary;
using Parquet.Extensions;

namespace Parquet.Encodings;

/// <summary>
/// ALP (Adaptive Lossless floating-Point, encoding 10) for FLOAT and DOUBLE, decoding only.
/// Spec: https://github.com/apache/parquet-format/blob/master/AlpEncoding.md
/// <para>
/// A page is a 7 byte header (mode, integer encoding, log2 of vector size, value count), an offset per vector, and
/// the vectors. A vector stores integers as frame of reference + bit-packed deltas; each decodes as
/// <c>integer * 10^factor * 10^-exponent</c>, and values that do not round-trip are stored verbatim as exceptions.
/// </para>
/// </summary>
static class AlpEncoder {

    private const int PageHeaderSize = 7;

    // The spec requires these exact (correctly rounded) literals; Math.Pow is not guaranteed to produce them.
    private static readonly float[] FloatPow10 = [1e0f, 1e1f, 1e2f, 1e3f, 1e4f, 1e5f, 1e6f, 1e7f, 1e8f, 1e9f, 1e10f];
    private static readonly float[] FloatNegPow10 = [1e0f, 1e-1f, 1e-2f, 1e-3f, 1e-4f, 1e-5f, 1e-6f, 1e-7f, 1e-8f, 1e-9f, 1e-10f];
    private static readonly double[] DoublePow10 = [
        1e0, 1e1, 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9, 1e10, 1e11, 1e12, 1e13, 1e14, 1e15, 1e16, 1e17, 1e18];
    private static readonly double[] DoubleNegPow10 = [
        1e0, 1e-1, 1e-2, 1e-3, 1e-4, 1e-5, 1e-6, 1e-7, 1e-8, 1e-9, 1e-10, 1e-11, 1e-12, 1e-13, 1e-14, 1e-15, 1e-16, 1e-17, 1e-18];

    private delegate void VectorDecoder<T>(Span<byte> vector, Span<T> output, long[] deltas);

    /// <summary>
    /// Decodes one ALP page into <paramref name="dest"/> and returns the number of values written.
    /// </summary>
    public static int Decode<T>(Span<byte> source, Span<T> dest) where T : struct {
        if(typeof(T) == typeof(float))
            return DecodePage(source, dest.AsSpan<T, float>(), DecodeFloatVector);
        if(typeof(T) == typeof(double))
            return DecodePage(source, dest.AsSpan<T, double>(), DecodeDoubleVector);
        throw new ParquetException($"ALP encoding only applies to FLOAT and DOUBLE, not {typeof(T).Name}");
    }

    private static int DecodePage<T>(Span<byte> page, Span<T> dest, VectorDecoder<T> decodeVector) {
        Check(page.Length >= PageHeaderSize, "page is shorter than the ALP header");
        byte mode = page[0], integerEncoding = page[1], logVectorSize = page[2];
        int count = BinaryPrimitives.ReadInt32LittleEndian(page.Slice(3));
        if((mode != 0) || (integerEncoding != 0))
            throw new ParquetException($"ALP compression mode {mode} with integer encoding {integerEncoding} is not supported");
        Check((logVectorSize >= 3) && (logVectorSize <= 15), $"invalid vector size 2^{logVectorSize}");
        Check((count >= 0) && (count <= dest.Length), $"page claims {count} values, expected at most {dest.Length}");

        int vectorSize = 1 << logVectorSize;
        int vectorCount = (count + vectorSize - 1) / vectorSize;
        Span<byte> vectors = page.Slice(PageHeaderSize);    // offsets are relative to here
        Check(vectors.Length >= (vectorCount * sizeof(uint)), "offset array is truncated");

        long[] deltas = ArrayPool<long>.Shared.Rent(vectorSize);
        try {
            for(int v = 0; v < vectorCount; v++) {
                uint offset = BinaryPrimitives.ReadUInt32LittleEndian(vectors.Slice(v * sizeof(uint)));
                Check(offset <= vectors.Length, $"vector {v} starts past the end of the page");
                int start = v * vectorSize;
                decodeVector(vectors.Slice((int)offset), dest.Slice(start, Math.Min(vectorSize, count - start)), deltas);
            }
        } finally {
            ArrayPool<long>.Shared.Return(deltas);
        }
        return count;
    }

    // Vector: AlpInfo (exponent, factor, exception count: 4 bytes), ForInfo (frame of reference, bit width),
    // then packed deltas, exception positions and exception values (raw IEEE 754 bits).

    private static void DecodeFloatVector(Span<byte> vector, Span<float> output, long[] deltas) {
        Check(vector.Length >= 9, "float vector header is truncated");
        (int exponent, int factor, int exceptions) = ReadAlpInfo(vector, maxExponent: 10);
        int reference = BinaryPrimitives.ReadInt32LittleEndian(vector.Slice(4));
        Span<byte> data = vector.Slice(9);
        Span<byte> exceptionData = UnpackDeltas(data, bitWidth: vector[8], maxBitWidth: 32, output.Length, deltas);

        float pow = FloatPow10[factor], negPow = FloatNegPow10[exponent];
        for(int i = 0; i < output.Length; i++) {
            // two separate single precision multiplications, as the spec requires
            float value = unchecked((int)deltas[i] + reference);
            value *= pow;
            value *= negPow;
            output[i] = value;
        }

        Span<byte> values = SplitExceptions(exceptionData, exceptions, sizeof(float), output.Length, out Span<byte> positions);
        for(int j = 0; j < exceptions; j++) {
            int bits = BinaryPrimitives.ReadInt32LittleEndian(values.Slice(j * sizeof(float)));
            output[BinaryPrimitives.ReadUInt16LittleEndian(positions.Slice(j * sizeof(ushort)))] = BitConverter.Int32BitsToSingle(bits);
        }
    }

    private static void DecodeDoubleVector(Span<byte> vector, Span<double> output, long[] deltas) {
        Check(vector.Length >= 13, "double vector header is truncated");
        (int exponent, int factor, int exceptions) = ReadAlpInfo(vector, maxExponent: 18);
        long reference = BinaryPrimitives.ReadInt64LittleEndian(vector.Slice(4));
        Span<byte> data = vector.Slice(13);
        Span<byte> exceptionData = UnpackDeltas(data, bitWidth: vector[12], maxBitWidth: 64, output.Length, deltas);

        double pow = DoublePow10[factor], negPow = DoubleNegPow10[exponent];
        for(int i = 0; i < output.Length; i++) {
            double value = unchecked(deltas[i] + reference);
            output[i] = value * pow * negPow;
        }

        Span<byte> values = SplitExceptions(exceptionData, exceptions, sizeof(double), output.Length, out Span<byte> positions);
        for(int j = 0; j < exceptions; j++) {
            long bits = BinaryPrimitives.ReadInt64LittleEndian(values.Slice(j * sizeof(double)));
            output[BinaryPrimitives.ReadUInt16LittleEndian(positions.Slice(j * sizeof(ushort)))] = BitConverter.Int64BitsToDouble(bits);
        }
    }

    private static (int Exponent, int Factor, int Exceptions) ReadAlpInfo(Span<byte> vector, int maxExponent) {
        int exponent = vector[0], factor = vector[1];
        Check((exponent <= maxExponent) && (factor <= exponent), $"invalid exponent {exponent} / factor {factor}");
        return (exponent, factor, BinaryPrimitives.ReadUInt16LittleEndian(vector.Slice(2)));
    }

    /// <summary>
    /// Unpacks <paramref name="count"/> LSB-first deltas into <paramref name="deltas"/> and returns the bytes after them.
    /// </summary>
    private static Span<byte> UnpackDeltas(Span<byte> data, int bitWidth, int maxBitWidth, int count, long[] deltas) {
        Check(bitWidth <= maxBitWidth, $"invalid bit width {bitWidth}");
        int packedLength = (int)(((long)count * bitWidth + 7) / 8);
        Check(data.Length >= packedLength, "packed values are truncated");

        // groups of 8; the last group is usually partial, so unpack it from a zero padded copy
        Span<byte> paddedGroup = stackalloc byte[64];
        Span<long> group = stackalloc long[8];
        for(int i = 0; i < count; i += 8) {
            scoped Span<byte> packed = data.Slice(i / 8 * bitWidth, Math.Min(bitWidth, packedLength - (i / 8 * bitWidth)));
            if(packed.Length < bitWidth) {
                paddedGroup.Clear();
                packed.CopyTo(paddedGroup);
                packed = paddedGroup;
            }
            BitPackedEncoder.Unpack8ValuesLE(packed, group, bitWidth);
            group.Slice(0, Math.Min(8, count - i)).CopyTo(deltas.AsSpan(i));
        }
        return data.Slice(packedLength);
    }

    /// <summary>
    /// Splits the exception data into positions and values, checking both fit and every position is in the vector.
    /// </summary>
    private static Span<byte> SplitExceptions(Span<byte> exceptionData, int exceptions, int valueSize, int count,
        out Span<byte> positions) {
        Check(exceptionData.Length >= (exceptions * (sizeof(ushort) + valueSize)), "exceptions are truncated");
        positions = exceptionData.Slice(0, exceptions * sizeof(ushort));
        for(int j = 0; j < exceptions; j++)
            Check(BinaryPrimitives.ReadUInt16LittleEndian(positions.Slice(j * sizeof(ushort))) < count, "exception position is outside the vector");
        return exceptionData.Slice(positions.Length);
    }

    private static void Check(bool condition, string problem) {
        if(!condition)
            throw new ParquetException($"invalid ALP data: {problem}");
    }
}
