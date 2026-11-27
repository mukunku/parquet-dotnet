using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Parquet.Data;
using Parquet.Encodings;
using Parquet.Meta;
using Parquet.Schema;
using Xunit;

namespace Parquet.Test.Encodings;

public class ByteStreamSplitEncoderTest : TestBase {

    [Fact]
    public void TestApacheExample() {
        /*
        * Example: Original data is three 32-bit floats and for simplicity we look at their raw representation.
        *        Element 0      Element 1      Element 2
        * Bytes  AA BB CC DD    00 11 22 33    A3 B4 C5 D6
        * 
        * After applying the transformation, the data has the following representation:
        * Bytes  AA 00 A3 BB 11 B4 CC 22 C5 DD 33 D6
        */

        float[] expected = [FromHex("AABBCCDD"), FromHex("00112233"), FromHex("A3B4C5D6")];
        byte[] bytes = ToByteArray("AA00A3BB11B4CC22C5DD33D6");

        float[] dest = new float[3];
        ByteStreamSplitEncoder.Decode(bytes, dest);
        for(int i = 0; i < 3; i++) {
            Assert.Equal(expected[i], dest[i]);
        }

        float FromHex(string hex) {
            Span<byte> b = stackalloc byte[4];
            for(int i = 0; i < 4; i++) {
                b[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return BitConverter.ToSingle(b);
        }
        byte[] ToByteArray(string hex) {
            int len = hex.Length / 2;
            byte[] b = new byte[len];
            for(int i = 0; i < len; i++) {
                b[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return b;
        }
    }

    [Fact]
    public void TestApacheExampleEncoding() {
        /*
         * Encoding test based on TestApacheExample data.
         * Original three 32-bit floats (raw bytes):
         *        Element 0      Element 1      Element 2
         * Bytes  AA BB CC DD    00 11 22 33    A3 B4 C5 D6
         * 
         * After encoding, should produce byte-stream-split format:
         * Bytes  AA 00 A3 BB 11 B4 CC 22 C5 DD 33 D6
         */

        float[] source = [FromHex("AABBCCDD"), FromHex("00112233"), FromHex("A3B4C5D6")];
        byte[] expected = ToByteArray("AA00A3BB11B4CC22C5DD33D6");

        using var ms = new MemoryStream();
        ByteStreamSplitEncoder.Encode(source, ms);

        Assert.Equal(expected, ms.ToArray());

        float FromHex(string hex) {
            Span<byte> b = stackalloc byte[4];
            for(int i = 0; i < 4; i++) {
                b[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return BitConverter.ToSingle(b);
        }
        byte[] ToByteArray(string hex) {
            int len = hex.Length / 2;
            byte[] b = new byte[len];
            for(int i = 0; i < len; i++) {
                b[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return b;
        }
    }

    [Theory]
    [InlineData("byte_stream_split_256.parquet")]
    public async Task TestFloatDoubleValues(string parquetFile) {
        /*
         * 256 records, two columns value and fvalue
         * Each row value is index * 1.5.
         */
        await using ParquetReader reader = await ParquetReader.CreateAsync(OpenTestFile(parquetFile), leaveStreamOpen: false);
        Assert.Equal(1, reader.RowGroupCount);
        ParquetRowGroupReader rgr = reader.OpenRowGroupReader(0);

        DataField doubleField = reader.Schema.FindDataField("value");
        DataField floatField = reader.Schema.FindDataField("fvalue");

        using RawColumnData<double> doubleCol = await rgr.ReadRawColumnDataAsync<double>(doubleField);
        using RawColumnData<float> floatCol = await rgr.ReadRawColumnDataAsync<float>(floatField);

        Assert.Equal(256, doubleCol.Values.Length);
        Assert.Equal(256, floatCol.Values.Length);

        for(int i = 0; i < 256; i++) {
            double d = i * 1.5d;
            float f = i * 1.5f;
            Assert.Equal(d, (double)doubleCol.Values[i], 5);
            Assert.Equal(f, (float)floatCol.Values[i], 5);
        }
    }

    [Theory]
    [InlineData("bss_with_nulls_double.parquet")]
    [InlineData("bss_with_nulls_float.parquet")]
    public async Task TestNullValues(string parquetFile) {
        /*
         * 5 records, column named floats with values [1.1, null, 3.3, null, 5.5]
         */
        await using ParquetReader reader = await ParquetReader.CreateAsync(OpenTestFile(parquetFile), leaveStreamOpen: false);
        Assert.Equal(1, reader.RowGroupCount);
        using ParquetRowGroupReader row = reader.OpenRowGroupReader(0);

        DataField floatsField = reader.Schema.FindDataField("floats");

        if(parquetFile.Contains("double")) {
            double?[] dataValues = new double?[row.RowCount];
            await row.ReadAsync<double>(floatsField, dataValues);

            Assert.Equal(5, dataValues.Length);
            Assert.Equal(3, dataValues.Count(v => v.HasValue));

            double[] definedValues = dataValues.Where(v => v.HasValue).Select(v => v!.Value).ToArray();
            Assert.Equal(1.1, definedValues[0]);
            Assert.Equal(3.3, definedValues[1]);
            Assert.Equal(5.5, definedValues[2]);

            Assert.Equal(1.1, dataValues[0]);
            Assert.Null(dataValues[1]);
            Assert.Equal(3.3, dataValues[2]);
            Assert.Null(dataValues[3]);
            Assert.Equal(5.5, dataValues[4]);
        } else {
            float?[] dataValues = new float?[row.RowCount];
            await row.ReadAsync<float>(floatsField, dataValues);

            Assert.Equal(5, dataValues.Length);
            Assert.Equal(3, dataValues.Count(v => v.HasValue));

            float[] definedValues = dataValues.Where(v => v.HasValue).Select(v => v!.Value).ToArray();
            Assert.Equal(1.1f, definedValues[0]);
            Assert.Equal(3.3f, definedValues[1]);
            Assert.Equal(5.5f, definedValues[2]);

            Assert.Equal(1.1f, dataValues[0]);
            Assert.Null(dataValues[1]);
            Assert.Equal(3.3f, dataValues[2]);
            Assert.Null(dataValues[3]);
            Assert.Equal(5.5f, dataValues[4]);
        }
    }


    private async Task EncodingHint_Respected<T>(T[] sample) where T : struct {
        var dataField = new DataField<T>("id");
        var parquetSchema = new ParquetSchema(dataField);

        using var stream = new MemoryStream();

        var options = new ParquetOptions();
        options.ColumnEncodingHints["id"] = EncodingHint.ByteSplitStream;
        await using(ParquetWriter parquetWriter = await ParquetWriter.CreateAsync(parquetSchema, stream, options: options)) {
            using ParquetRowGroupWriter groupWriter = parquetWriter.CreateRowGroup();
            await groupWriter.WriteAsync<T>(dataField, sample);
        }

        await using ParquetReader rdr = await ParquetReader.CreateAsync(stream);
        ParquetRowGroupReader rgr = rdr.OpenRowGroupReader(0);
        using RawColumnData<T> col = await rgr.ReadRawColumnDataAsync<T>(dataField);

        Assert.Equal(sample, col.Values.ToArray());

        // check encoding
        ColumnChunk? meta = rgr.GetMetadata(dataField);
        Assert.NotNull(meta);
        Assert.Contains(Encoding.BYTE_STREAM_SPLIT, meta.MetaData!.Encodings);
    }

    [Fact]
    public async Task EncodingHint_Respected_float() {
        await EncodingHint_Respected<float>(Enumerable.Range(0, 100).Select(i => (float)i).ToArray());
    }

    [Fact]
    public async Task EncodingHint_Respected_double() {
        await EncodingHint_Respected<double>(Enumerable.Range(0, 100).Select(i => (double)i).ToArray());
    }

    [Fact]
    public async Task EncodingHint_Respected_int() {
        await EncodingHint_Respected<int>(Enumerable.Range(0, 100).ToArray());
    }

    [Fact]
    public async Task EncodingHint_Respected_long() {
        await EncodingHint_Respected<long>(Enumerable.Range(0, 100).Select(i => (long)i).ToArray());
    }

    [Fact]
    public async Task EncodingHint_Respected_short() {
        await EncodingHint_Respected<short>(Enumerable.Range(0, 100).Select(i => (short)i).ToArray());
    }

    [Fact]
    public async Task EncodingHint_Respected_ushort() {
        await EncodingHint_Respected<ushort>(Enumerable.Range(0, 100).Select(i => (ushort)i).ToArray());
    }

    [Fact]
    public async Task EncodingHint_Respected_uint() {
        await EncodingHint_Respected<uint>(Enumerable.Range(0, 100).Select(i => (uint)i).ToArray());
    }

    [Fact]
    public async Task EncodingHint_Respected_ulong() {
        await EncodingHint_Respected<ulong>(Enumerable.Range(0, 100).Select(i => (ulong)i).ToArray());
    }

    [Fact]
    public void DecodeToPlain_ReversesApacheExample() {
        // three 4-byte values AA BB CC DD | 00 11 22 33 | A3 B4 C5 D6, split into byte streams
        byte[] split = [0xAA, 0x00, 0xA3, 0xBB, 0x11, 0xB4, 0xCC, 0x22, 0xC5, 0xDD, 0x33, 0xD6];
        byte[] plain = new byte[12];
        ByteStreamSplitEncoder.DecodeToPlain(split, plain, 4, 3);
        Assert.Equal(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0x00, 0x11, 0x22, 0x33, 0xA3, 0xB4, 0xC5, 0xD6 }, plain);
    }

    /*
     * bss_logical_types.parquet: pyarrow 23.0.1, data page V2, snappy, BYTE_STREAM_SPLIT on every column,
     * store_decimal_as_integer=True. 5 rows, row 3 (index 2) is null in every column.
     * Before the fix, every column whose CLR type is converted from the physical value failed with
     * NotSupportedException carrying just the type name, e.g. "DateTime".
     */
    private const string LogicalTypesFile = "bss_logical_types.parquet";

    [Fact]
    public async Task LogicalTypes_DefaultOptions() {
        await using ParquetReader reader = await ParquetReader.CreateAsync(OpenTestFile(LogicalTypesFile), leaveStreamOpen: false);
        using ParquetRowGroupReader rgr = reader.OpenRowGroupReader(0);

        DateTime?[] tsUs = await ReadNullableValuesAsync<DateTime>(rgr, reader.Schema.FindDataField("ts_us"));
        Assert.Equal(new DateTime?[] {
            new DateTime(2024, 5, 17, 8, 30, 15).AddTicks(1_234_560), new DateTime(1969, 12, 31, 23, 59, 59), null,
            new DateTime(9999, 12, 31), new DateTime(1, 1, 1)
        }, tsUs);

        DateTime?[] tsMsUtc = await ReadNullableValuesAsync<DateTime>(rgr, reader.Schema.FindDataField("ts_ms_utc"));
        Assert.Equal(new DateTime?[] {
            new DateTime(2024, 5, 17, 8, 30, 15, 123), new DateTime(1969, 12, 31, 23, 59, 59), null,
            new DateTime(2262, 1, 1), new DateTime(1900, 1, 1)
        }, tsMsUtc);
        Assert.All(tsMsUtc.Where(v => v.HasValue), v => Assert.Equal(DateTimeKind.Utc, v!.Value.Kind));

        DateTime?[] dates = await ReadNullableValuesAsync<DateTime>(rgr, reader.Schema.FindDataField("date"));
        Assert.Equal(new DateTime?[] {
            new DateTime(2024, 5, 17), new DateTime(1969, 12, 31), null, new DateTime(9999, 12, 31), new DateTime(1, 1, 1)
        }, dates);

        decimal?[] decI32 = await ReadNullableValuesAsync<decimal>(rgr, reader.Schema.FindDataField("dec_i32"));
        Assert.Equal(new decimal?[] { 1.23m, -4.56m, null, 9999999.99m, 0m }, decI32);

        decimal?[] decI64 = await ReadNullableValuesAsync<decimal>(rgr, reader.Schema.FindDataField("dec_i64"));
        Assert.Equal(new decimal?[] { 1.23m, -4.56m, null, 9999999999999999.99m, 0m }, decI64);

        decimal?[] decFlba = await ReadNullableValuesAsync<decimal>(rgr, reader.Schema.FindDataField("dec_flba"));
        Assert.Equal(new decimal?[] { 1.23m, -4.56m, null, 12345678901234567890.12m, 0m }, decFlba);

        ReadOnlyMemory<byte>?[] flba = await ReadNullableValuesAsync<ReadOnlyMemory<byte>>(rgr, reader.Schema.FindDataField("flba16"));
        Assert.Null(flba[2]);
        Assert.Equal(new byte?[] { 1, 2, null, 3, 4 }, flba.Select(v => v.HasValue ? v.Value.Span[15] : (byte?)null));
        Assert.All(flba.Where(v => v.HasValue), v => Assert.Equal(16, v!.Value.Length));

        // types that were already supported keep working
        int?[] i32 = await ReadNullableValuesAsync<int>(rgr, reader.Schema.FindDataField("i32"));
        Assert.Equal(new int?[] { 1, -2, null, 2_000_000_000, 7 }, i32);
        long?[] timeUs = await ReadNullableValuesAsync<long>(rgr, reader.Schema.FindDataField("time_us"));
        Assert.Equal(new long?[] { 37_800_000_000, 0, null, 86_399_999_999, 1_000_000 }, timeUs);
    }

    [Fact]
    public async Task LogicalTypes_DateOnlyAndBigDecimal() {
        await using ParquetReader reader = await ParquetReader.CreateAsync(OpenTestFile(LogicalTypesFile),
            new ParquetOptions { UseDateOnlyTypeForDates = true, UseBigDecimal = true }, leaveStreamOpen: false);
        using ParquetRowGroupReader rgr = reader.OpenRowGroupReader(0);

        DateOnly?[] dates = await ReadNullableValuesAsync<DateOnly>(rgr, reader.Schema.FindDataField("date"));
        Assert.Equal(new DateOnly?[] {
            new DateOnly(2024, 5, 17), new DateOnly(1969, 12, 31), null, new DateOnly(9999, 12, 31), new DateOnly(1, 1, 1)
        }, dates);

        BigDecimal?[] decFlba = await ReadNullableValuesAsync<BigDecimal>(rgr, reader.Schema.FindDataField("dec_flba"));
        Assert.Equal(new string?[] { "1.23", "-4.56", null, "12345678901234567890.12", "0.00" },
            decFlba.Select(v => v?.ToString()));
    }

}
