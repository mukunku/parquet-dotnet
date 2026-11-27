using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Parquet.Encodings;
using Parquet.Schema;
using Xunit;

namespace Parquet.Test.Encodings;

public class AlpEncoderTest : TestBase {

    /// <summary>
    /// The worked example from AlpEncoding.md: { 1500.0, NaN, 2500.0, 333.5 } with exponent 4, factor 3,
    /// frame of reference 3335, bit width 15 and the NaN stored as an exception.
    /// </summary>
    [Fact]
    public void Decode_SpecWorkedExample() {
        const long nanBits = 0x7FF8_0000_0000_0123;     // NaN with a payload, which must survive untouched

        var page = new MemoryStream();
        page.Write([0, 0, 10]);                         // compression mode ALP, FOR + bit-packing, vector size 2^10
        page.Write(Int32(4));                            // values in the page
        page.Write(UInt32(4));                           // offset of vector 0, just past the one entry offset array
        page.Write([4, 3]);                              // exponent, factor
        page.Write(UInt16(1));                           // exceptions
        page.Write(Int64(3335));                         // frame of reference
        page.WriteByte(15);                              // bit width
        page.Write(PackLsbFirst([11665, 11665, 21665, 0], 15));
        page.Write(UInt16(1));                           // exception position
        page.Write(Int64(nanBits));                      // exception value

        var decoded = new double[4];
        Assert.Equal(4, AlpEncoder.Decode(page.ToArray().AsSpan(), decoded.AsSpan()));
        Assert.Equal(1500.0, decoded[0]);
        Assert.Equal(nanBits, BitConverter.DoubleToInt64Bits(decoded[1]));
        Assert.Equal(2500.0, decoded[2]);
        Assert.Equal(333.5, decoded[3]);
    }

    [Fact]
    public void Decode_UnsupportedMode_Throws() {
        byte[] page = [1, 0, 10, .. Int32(0)];          // compression mode 1 is reserved (e.g. ALP-RD)
        ParquetException ex = Assert.Throws<ParquetException>(() => AlpEncoder.Decode(page.AsSpan(), new double[1].AsSpan()));
        Assert.Contains("compression mode 1", ex.Message);
    }

    /// <summary>
    /// alp_extended.zstd.parquet from https://github.com/apache/parquet-testing (parquet-cpp-arrow 23.0.0-SNAPSHOT):
    /// float and double columns encoded with ALP at vector sizes 32, 1024 and 4096 next to PLAIN encoded copies of the
    /// same data, over 5 row groups with nulls, NaN, infinities, constant runs and values too large to convert.
    /// Every ALP value must match its PLAIN twin bit for bit.
    /// </summary>
    [Theory]
    [InlineData("float", 32)]
    [InlineData("float", 1024)]
    [InlineData("float", 4096)]
    [InlineData("double", 32)]
    [InlineData("double", 1024)]
    [InlineData("double", 4096)]
    public async Task Decode_ReferenceFile_MatchesPlain(string type, int vectorSize) {
        await using ParquetReader reader = await ParquetReader.CreateAsync(OpenTestFile("alp_extended.zstd.parquet"));
        DataField plain = reader.Schema.DataFields.Single(f => f.Name == $"{type}_plain");
        DataField alp = reader.Schema.DataFields.Single(f => f.Name == $"{type}_alp_{vectorSize}");

        for(int g = 0; g < reader.RowGroupCount; g++) {
            using ParquetRowGroupReader rgr = reader.OpenRowGroupReader(g);
            if(type == "float") {
                float?[] expected = await ReadNullableValuesAsync<float>(rgr, plain);
                float?[] actual = await ReadNullableValuesAsync<float>(rgr, alp);
                Assert.Equal(expected.Select(v => v.HasValue ? BitConverter.SingleToInt32Bits(v.Value) : (int?)null),
                    actual.Select(v => v.HasValue ? BitConverter.SingleToInt32Bits(v.Value) : (int?)null));
            } else {
                double?[] expected = await ReadNullableValuesAsync<double>(rgr, plain);
                double?[] actual = await ReadNullableValuesAsync<double>(rgr, alp);
                Assert.Equal(expected.Select(v => v.HasValue ? BitConverter.DoubleToInt64Bits(v.Value) : (long?)null),
                    actual.Select(v => v.HasValue ? BitConverter.DoubleToInt64Bits(v.Value) : (long?)null));
            }
        }
    }

    private static byte[] Int32(int v) { byte[] b = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(b, v); return b; }
    private static byte[] UInt32(uint v) { byte[] b = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(b, v); return b; }
    private static byte[] UInt16(ushort v) { byte[] b = new byte[2]; BinaryPrimitives.WriteUInt16LittleEndian(b, v); return b; }
    private static byte[] Int64(long v) { byte[] b = new byte[8]; BinaryPrimitives.WriteInt64LittleEndian(b, v); return b; }

    private static byte[] PackLsbFirst(ulong[] values, int bitWidth) {
        byte[] packed = new byte[((values.Length * bitWidth) + 7) / 8];
        for(int i = 0; i < values.Length; i++)
            for(int bit = 0; bit < bitWidth; bit++)
                if(((values[i] >> bit) & 1) != 0) {
                    int position = (i * bitWidth) + bit;
                    packed[position / 8] |= (byte)(1 << (position % 8));
                }
        return packed;
    }
}
