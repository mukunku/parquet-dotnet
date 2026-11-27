using System;
using System.Linq;
using Parquet.Encodings;
using Xunit;

namespace Parquet.Test.Encodings;

public class BitPackedEncoderTest {

    const string Binary0to7WithBitWidth3LE = "10001000 11000110 11111010";
    const string Binary0to7WithBitWidth3BE = "00000101 00111001 01110111";

    private byte[] BytesFromBinaryString(string s) {
        return s.Split(' ').Select(s => Convert.ToByte(s, 2)).ToArray();
    }

    [Fact]
    public void Encode1to7WithBitWidth3BE() {
        byte[] dest = new byte[3];
        BitPackedEncoder.Encode8ValuesBE(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, dest, 3);

        Assert.Equal(BytesFromBinaryString(Binary0to7WithBitWidth3BE), dest.ToArray());
    }

    [Fact]
    public void Decode1to7WithBitWidthBE() {
        Span<int> ints = new int[8];
        BitPackedEncoder.Decode8ValuesBE(BytesFromBinaryString(Binary0to7WithBitWidth3BE).AsSpan(), ints, 3);
        Assert.Equal(new int[] { 0, 1, 2, 3, 4, 5, 6, 7 }, ints.ToArray());
    }

    [Fact]
    public void Encode1to7WithBitWidth3() {
        byte[] dest = new byte[3];
        BitPackedEncoder.Encode8ValuesLE(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, dest, 3);

        Assert.Equal(BytesFromBinaryString(Binary0to7WithBitWidth3LE), dest.ToArray());
    }

    [Fact]
    public void Decode1to7WithBitWidth3() {
        Span<int> ints = new int[8];
        BitPackedEncoder.Decode8ValuesLE(BytesFromBinaryString(Binary0to7WithBitWidth3LE).AsSpan(), ints, 3);
        Assert.Equal(new int[] { 0, 1, 2, 3, 4, 5, 6, 7 }, ints.ToArray());
    }

    // used to throw "Destination is too short", copying all of src into a bitWidth sized buffer
    [Fact]
    public void Decode8ValuesLE_short_dest_long_src() {
        byte[] src = [.. BytesFromBinaryString(Binary0to7WithBitWidth3LE), 0xFF, 0xFF];
        int[] ints = new int[5];
        Assert.Equal(5, BitPackedEncoder.Decode8ValuesLE(src, ints, 3));
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, ints);

        long[] longs = new long[5];
        Assert.Equal(5, BitPackedEncoder.Decode8ValuesLE(src, longs, 3));
        Assert.Equal(new long[] { 0, 1, 2, 3, 4 }, longs);
    }

    // values only partly in src are not returned
    [Fact]
    public void Decode8ValuesLE_short_src() {
        byte[] src = BytesFromBinaryString(Binary0to7WithBitWidth3LE)[..2];   // 16 bits: 5 whole values
        int[] ints = new int[8];
        Assert.Equal(5, BitPackedEncoder.Decode8ValuesLE(src, ints, 3));
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 0, 0, 0 }, ints);
    }

    // used to divide by zero when dest is shorter than 8
    [Fact]
    public void Decode8ValuesLE_bit_width_0() {
        int[] ints = [9, 9, 9];
        Assert.Equal(3, BitPackedEncoder.Decode8ValuesLE([], ints, 0));
        Assert.Equal(new[] { 0, 0, 0 }, ints);

        long[] longs = [9, 9, 9];
        Assert.Equal(3, BitPackedEncoder.Decode8ValuesLE([], longs, 0));
        Assert.Equal(new long[] { 0, 0, 0 }, longs);
    }

    [Fact]
    public void Decode8ValuesLE_invalid_bit_width_throws() {
        Assert.Throws<ArgumentOutOfRangeException>(() => BitPackedEncoder.Decode8ValuesLE(new byte[40], new int[8], 33));
        Assert.Throws<ArgumentOutOfRangeException>(() => BitPackedEncoder.Decode8ValuesLE(new byte[8], new long[8], -1));
    }
}
