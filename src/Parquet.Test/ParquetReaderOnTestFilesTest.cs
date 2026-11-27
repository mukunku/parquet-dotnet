using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Parquet.Data;
using Parquet.Encodings;
using Parquet.Schema;
using Parquet.Serialization;
using Parquet.Serialization.Attributes;
using Xunit;

namespace Parquet.Test;

/// <summary>
/// Tests a set of predefined test files that they read back correct. Find more test data (some taken from there):
/// https://github.com/apache/parquet-testing/tree/master/data
/// </summary>
public class ParquetReaderOnTestFilesTest : TestBase {

    [Theory]
    [InlineData("fixedlenbytearray.parquet")]
    [InlineData("fixedlenbytearray.v2.parquet")]
    public async Task FixedLenByteArray_dictionary(string parquetFile) {
        await using Stream s = OpenTestFile(parquetFile);
        await using ParquetReader r = await ParquetReader.CreateAsync(s);

        DeserializationResult<Dictionary<string, object>> result = await ParquetSerializer.DeserializeUntypedAsync(s);
        IList<Dictionary<string, object>>? data = result.Data;
        ParquetSchema? schema = result.Schema;
    }

    [Theory]
    [InlineData("dates.parquet")]
    [InlineData("dates.v2.parquet")]
    public async Task Datetypes_all(string parquetFile) {
        using Stream s = OpenTestFile(parquetFile);

        await using ParquetReader r = await ParquetReader.CreateAsync(s);
        using ParquetRowGroupReader rgr = r.OpenRowGroupReader(0);

        var col0 = new DateTime?[rgr.RowGroup.NumRows];
        var col1 = new DateTime?[rgr.RowGroup.NumRows];
        await rgr.ReadAsync<DateTime>(r.Schema.DataFields[0], col0);
        await rgr.ReadAsync<DateTime>(r.Schema.DataFields[1], col1);

        DateTime? o0 = col0[0];
        DateTime? o1 = col0[1];

        Assert.Equal(new DateTime(2017, 1, 1), o0);
        Assert.Equal(new DateTime(2017, 2, 1), o1);
    }

    [Theory]
    [InlineData("datetime_other_system.parquet")]
    [InlineData("datetime_other_system.v2.parquet")]
    public async Task DateTime_FromOtherSystem(string parquetFile) {
        using(Stream s = OpenTestFile(parquetFile)) {
            await using(ParquetReader r = await ParquetReader.CreateAsync(s)) {

                using ParquetRowGroupReader rgr = r.OpenRowGroupReader(0);

                DataField? field = r.Schema.GetDataFields().FirstOrDefault(x => x.Name == "as_at_date_");
                Assert.NotNull(field);

                var col = new DateTime?[rgr.RowGroup.NumRows];
                await rgr.ReadAsync<DateTime>(field, col);

                DateTime? offset = col[0];
                Assert.Equal(new DateTime(2018, 12, 14, 0, 0, 0), offset!.Value.Date);
            }
        }
    }

    [Fact]
    public async Task ReadLocicalTimeAllPrecisions() {
        await using Stream s = OpenTestFile("logical_time_all_precisions.parquet");
        await using ParquetReader r = await ParquetReader.CreateAsync(s);
        using ParquetRowGroupReader rgr = r.OpenRowGroupReader(0);
        
        DataField[] fields = r.Schema.GetDataFields();
        Assert.Equal(3, fields.Length);
        
        // there are 3 columns: time_ms, time_us, time_ns, each marking 10:30 in logical time
        
        DataField timeMsField = fields[0];
        DataField timeUsField = fields[1];
        DataField timeNsField = fields[2];
        
        Assert.Equal("time_ms", timeMsField.Name);
        Assert.Equal("time_us", timeUsField.Name);
        Assert.Equal("time_ns", timeNsField.Name);
        
        // check logical type annotations
        Assert.NotNull(timeMsField.SchemaElement?.LogicalType?.TIME);
        Assert.NotNull(timeUsField.SchemaElement?.LogicalType?.TIME);
        Assert.NotNull(timeNsField.SchemaElement?.LogicalType?.TIME);
        Assert.NotNull(timeMsField.SchemaElement?.LogicalType.TIME.Unit.MILLIS);
        Assert.NotNull(timeUsField.SchemaElement?.LogicalType?.TIME.Unit.MICROS);
        Assert.NotNull(timeNsField.SchemaElement?.LogicalType?.TIME.Unit.NANOS);
        
        // check column value
        int?[] msValues = new int?[1];
        await rgr.ReadAsync<int>(timeMsField, msValues.AsMemory());
        Assert.Equal(37800000, msValues[0]);
        
        long?[] usValues = new long?[1];
        await rgr.ReadAsync<long>(timeUsField, usValues.AsMemory());
        Assert.Equal(37800000000, usValues[0]);
        
        long?[] nsValues = new long?[1];
        await rgr.ReadAsync<long>(timeNsField, nsValues.AsMemory());
        Assert.Equal(37800000000000, nsValues[0]);
    }

    private async Task OptionalValues_WithoutStatistics(string parquetFile) {
        using(Stream s = OpenTestFile(parquetFile)) {
            await using(ParquetReader r = await ParquetReader.CreateAsync(s)) {
                using ParquetRowGroupReader rgr = r.OpenRowGroupReader(0);

                DataField? idField = r.Schema.GetDataFields().FirstOrDefault(x => x.Name == "id");
                DataField? valueField = r.Schema.GetDataFields().FirstOrDefault(x => x.Name == "value");
                Assert.NotNull(idField);
                Assert.NotNull(valueField);

                long?[] ids = await ReadNullableValuesAsync<long>(rgr, idField);
                int?[] values = await ReadNullableValuesAsync<int>(rgr, valueField);

                int index = Enumerable.Range(0, ids.Length)
                    .First(i => ids[i] == 20908539289);

                Assert.Equal(0, values[index]);
            }
        }
    }

    [Theory]
    [InlineData("issue-164.parquet")]
    [InlineData("issue-164.v2.parquet")]
    public async Task Issue164(string parquetFile) {
        using(Stream s = OpenTestFile(parquetFile)) {
            await using(ParquetReader r = await ParquetReader.CreateAsync(s)) {
                using ParquetRowGroupReader rgr = r.OpenRowGroupReader(0);
                DataField[] fields = r.Schema.GetDataFields();

                int?[] ids = await ReadNullableValuesAsync<int>(rgr, fields[0]);

                Assert.Contains(256779, ids);
            }
        }
    }

    [Fact]
    public async Task ByteArrayDecimal() {
        using Stream s = OpenTestFile("byte_array_decimal.parquet");
        await using ParquetReader r = await ParquetReader.CreateAsync(s);

        ParquetSchema schema = r.Schema;
        Assert.Equal("value", schema[0].Name);
        Assert.Single(schema.GetDataFields());

        using ParquetRowGroupReader rgr = r.OpenRowGroupReader(0);
        decimal?[] data = await ReadNullableValuesAsync<decimal>(rgr, schema.GetDataFields()[0]);

        Assert.Equal(
            Enumerable.Range(1, 24).Select(i => (decimal?)i),
            data);
    }

    [Fact]
    public async Task Read_delta_binary_packed() {
        using Stream s = OpenTestFile("delta_binary_packed.parquet");
        await using ParquetReader r = await ParquetReader.CreateAsync(s);

        ParquetSchema schema = r.Schema;

        using(ParquetRowGroupReader rgr = r.OpenRowGroupReader(0)) {
            DataField[] dfs = schema.GetDataFields();
            long[] values = await ReadValuesAsync<long>(rgr, dfs[1]);
            Assert.Equal(200, values.Length);
        }
    }


    [Fact]
    public async Task Read_legacy_list() {
        using Stream s = OpenTestFile("special/legacy-list.parquet");
        await using ParquetReader r = await ParquetReader.CreateAsync(s);
        using ParquetRowGroupReader rgr = r.OpenRowGroupReader(0);

        DataField[] fields = r.Schema.GetDataFields();
        Assert.Equal(3, fields.Length);

        string?[] col0 = await ReadStringValuesAsync(rgr, fields[0]);
        double[] col1 = await ReadValuesAsync<double>(rgr, fields[1]);

        Assert.Equal(new string[] { "1_0", "1_0" }, col0);
        Assert.Equal(new double[] { 2004, 2004 }, col1);
        Assert.Equal(336, GetValueBufferLength(rgr, fields[2]));
    }

    [Fact]
    public async Task Read_empty_and_null_lists() {
        using Stream s = OpenTestFile("list_empty_and_null.parquet");
        await using ParquetReader r = await ParquetReader.CreateAsync(s);

        DataField[] fields = r.Schema.GetDataFields();
        Assert.Equal(2, fields.Length);
    }

    [Fact]
    public async Task Wide() {
        using Stream s = OpenTestFile("special/wide.parquet");
        await using ParquetReader r = await ParquetReader.CreateAsync(s);
        using ParquetRowGroupReader rgr = r.OpenRowGroupReader(0);

        foreach(DataField field in r.Schema.GetDataFields()) {
            await ReadAnyFieldAsync(rgr, field);
        }
    }

    [Fact]
    public async Task Oracle_Int64_Field_With_Extra_Byte() {
        using Stream s = OpenTestFile("oracle_int64_extra_byte_at_end.parquet");
        await using ParquetReader r = await ParquetReader.CreateAsync(s);
        using ParquetRowGroupReader rgr = r.OpenRowGroupReader(0);

        DataField[] fields = r.Schema.GetDataFields();
        Assert.Equal(2, fields.Length);

        string[] col0 = (await ReadStringValuesAsync(rgr, fields[0])).Select(x => x!).ToArray();
        long[] col1 = await ReadValuesAsync<long>(rgr, fields[1]);

        Assert.Equal(126, col0.Length);
        Assert.Equal(126, col1.Length);
        Assert.Equal("DEPOSIT", col0[0]);
        Assert.Equal("DEPOSIT", col0[125]);
        Assert.Equal((long)1, col1[0]);
        Assert.Equal((long)1, col1[125]);
    }

    [Fact]
    public async Task FixedLenByteArrayWithDictTest() {
        using Stream s = OpenTestFile("fixed_len_byte_array_with_dict.parquet");
        await using ParquetReader r = await ParquetReader.CreateAsync(s);
        using ParquetRowGroupReader rgr = r.OpenRowGroupReader(0);

        DataField[] fields = r.Schema.GetDataFields();
        Assert.Equal(10, fields.Length);

        // last column is a dictionary-encoded FIXED_LEN_BYTE_ARRAY.
        byte[][] data = await ReadBinaryValuesAsync(rgr, fields[9]);
        Assert.Equal(6, data.Length);
        Assert.Equal(Encoding.ASCII.GetBytes("abc"), data[0]);
        Assert.Equal(Encoding.ASCII.GetBytes("def"), data[1]);
        Assert.Equal(Encoding.ASCII.GetBytes("ghi"), data[2]);
        Assert.Equal(Encoding.ASCII.GetBytes("jkl"), data[3]);
        Assert.Equal(Encoding.ASCII.GetBytes("mno"), data[4]);
        Assert.Equal(Encoding.ASCII.GetBytes("qrs"), data[5]);
    }

    [Fact]
    public async Task GuidEndianTest() {
        using Stream s = OpenTestFile("cetas4.parquet");
        await using ParquetReader r = await ParquetReader.CreateAsync(s);
        using ParquetRowGroupReader rgr = r.OpenRowGroupReader(0);

        DataField[] fields = r.Schema.GetDataFields();
        Assert.Single(fields);

        Guid?[] data = await ReadNullableValuesAsync<Guid>(rgr, fields[0]);
        Assert.Equal(Guid.Parse("15A2501E-4899-4FF8-AF51-A1805FE0718F"), data[0]);
    }

    [Fact]
    public async Task ThriftProtocolBreakingChangeJune2024() {
        using Stream s = OpenTestFile("thrift/breaking-spec-2024.parquet");
        await using ParquetReader r = await ParquetReader.CreateAsync(s);
        using ParquetRowGroupReader rgr = r.OpenRowGroupReader(0);

        DataField[] fields = r.Schema.GetDataFields();
        Assert.Equal(55, fields.Length);

        await ReadAnyFieldAsync(rgr, fields[0]);
    }

    [Fact]
    public async Task ThriftProtocolBreakingChangeJune2024_Untyped() {
        using Stream s = OpenTestFile("thrift/breaking-spec-2024.parquet");
        DeserializationResult<Dictionary<string, object>> result = await ParquetSerializer.DeserializeUntypedAsync(s);
    }

    [Fact]
    public async Task DecimalsWithNoDefinedScale() {
        using Stream s = OpenTestFile("decimals_with_precision_but_no_scale.parquet");
        await using ParquetReader r = await ParquetReader.CreateAsync(s);
        using ParquetRowGroupReader rgr = r.OpenRowGroupReader(0);

        DataField[] fields = r.Schema.GetDataFields();
        Assert.Equal(8, fields.Length);

        //DECIMAL(9, 5)
        decimal?[] data = await ReadNullableValuesAsync<decimal>(rgr, fields[5]);
        Assert.Equal(1.02232M, data[7]);
        Assert.Equal(-27.427M, data[344]);

        //DECIMAL(9, 0)
        data = await ReadNullableValuesAsync<decimal>(rgr, fields[6]);
        Assert.Equal(0M, data[0]);
        Assert.Equal(1000M, data[6616]);
    }

    [Fact]
    public async Task DuckDbRLE_637() {
        using Stream s = OpenTestFile("issues/637-duckdb.parquet");
        await using ParquetReader r = await ParquetReader.CreateAsync(s);
        using ParquetRowGroupReader rgr = r.OpenRowGroupReader(0);

        DataField[] fields = r.Schema.GetDataFields();
        Assert.Single(fields);

        int?[] data = await ReadNullableValuesAsync<int>(rgr, fields[0]);
        Assert.Equal(new int?[] { 2023, 2024 }, data);
    }

    /// <summary>
    /// TIME(NANOS) columns used to be plain-decoded as if they were microseconds, which either produced wrong values
    /// or threw "Ticks must be between 0 and TimeOnly.MaxValue.Ticks". File written by pyarrow (time64 us and ns,
    /// plain encoding, last value null).
    /// </summary>
    [Theory]
    [InlineData("time_us")]
    [InlineData("time_ns")]
    public async Task Time_Int64_AllUnits_PlainDecode_FromPyArrow(string columnName) {
        using Stream s = OpenTestFile("issues/time_units_pyarrow.parquet");
        await using ParquetReader r = await ParquetReader.CreateAsync(s);
        using ParquetRowGroupReader rgr = r.OpenRowGroupReader(0);

        DataField field = r.Schema.DataFields.First(f => f.Name == columnName);
        Assert.NotNull(field.SchemaElement);

        // raw values as stored in the file
        long?[] raw = await ReadNullableValuesAsync<long>(rgr, field);
        Assert.Null(raw[4]);
        byte[] source = MemoryMarshal.AsBytes(raw.Take(4).Select(v => v!.Value).ToArray().AsSpan()).ToArray();

        TimeOnly[] expected = [
            new TimeOnly(0, 0, 0),
            new TimeOnly(0, 1, 0),
            new TimeOnly(10, 30, 0),
            new TimeOnly(23, 59, 59, 999)
        ];

        var timeOnly = new TimeOnly[4];
        Assert.Equal(4, ParquetPlainEncoder.Decode(source.AsSpan(), timeOnly.AsSpan(), field.SchemaElement));
        Assert.Equal(expected, timeOnly);

        var timeSpan = new TimeSpan[4];
        Assert.Equal(4, ParquetPlainEncoder.Decode(source.AsSpan(), timeSpan.AsSpan(), field.SchemaElement));
        Assert.Equal(expected.Select(t => t.ToTimeSpan()), timeSpan);

        // and the encoder must write back exactly what pyarrow wrote
        using var ms = new MemoryStream();
        ParquetPlainEncoder.Encode(timeOnly, ms, field.SchemaElement);
        Assert.Equal(source, ms.ToArray());
        ms.SetLength(0);
        ParquetPlainEncoder.Encode(timeSpan, ms, field.SchemaElement);
        Assert.Equal(source, ms.ToArray());
    }

    class TimeMillisRecord {
        [ParquetTime(Precision = TimeUnitPrecision.Millis)]
        public TimeOnly? time_ms { get; set; }
    }

    /// <summary>
    /// Deserializing a TIME(MILLIS) column into <see cref="TimeOnly"/> used to fail with "No coercion operator is
    /// defined between types 'System.Int32' and 'System.Nullable`1[System.TimeOnly]'".
    /// </summary>
    [Fact]
    public async Task TimeOnly_Millis_Deserialize_FromPyArrow() {
        using Stream s = OpenTestFile("issues/time_units_pyarrow.parquet");
        IList<TimeMillisRecord> data = (await ParquetSerializer.DeserializeAsync<TimeMillisRecord>(s)).Data;

        Assert.Equal(new TimeOnly?[] {
            new TimeOnly(0, 0, 0),
            new TimeOnly(0, 1, 0),
            new TimeOnly(10, 30, 0),
            new TimeOnly(23, 59, 59, 999),
            null
        }, data.Select(d => d.time_ms));
    }

    class TimeMicrosRecord {
        public TimeOnly? time_us { get; set; }
    }

    /// <summary>
    /// Deserializing TIME(MICROS) into <see cref="TimeOnly"/> used to treat the stored value as .NET ticks,
    /// producing values 10x too small.
    /// </summary>
    [Fact]
    public async Task TimeOnly_Micros_Deserialize_FromPyArrow() {
        using Stream s = OpenTestFile("issues/time_units_pyarrow.parquet");
        IList<TimeMicrosRecord> data = (await ParquetSerializer.DeserializeAsync<TimeMicrosRecord>(s)).Data;

        Assert.Equal(new TimeOnly?[] {
            new TimeOnly(0, 0, 0),
            new TimeOnly(0, 1, 0),
            new TimeOnly(10, 30, 0),
            new TimeOnly(23, 59, 59, 999),
            null
        }, data.Select(d => d.time_us));
    }

    class TimeNanosIntoTimeOnlyRecord {
        public TimeOnly? time_ns { get; set; }
    }

    /// <summary>
    /// TIME(NANOS) and TimeOnly (micros) are both INT64, so a type check alone let the data be misread.
    /// </summary>
    [Fact]
    public async Task TimeOnly_Nanos_Deserialize_PrecisionMismatchThrows() {
        using Stream s = OpenTestFile("issues/time_units_pyarrow.parquet");
        InvalidCastException ex = await Assert.ThrowsAsync<InvalidCastException>(
            () => ParquetSerializer.DeserializeAsync<TimeNanosIntoTimeOnlyRecord>(s));
        Assert.Contains("precision", ex.Message);
        Assert.Contains("time_ns", ex.Message);
    }

    [Fact]
    public async Task HyparquetCompressed() {
        using Stream s = OpenTestFile("hyparquet.snappy.parquet");
        DeserializationResult<Dictionary<string, object>> result = await ParquetSerializer.DeserializeUntypedAsync(s);
    }

    [Fact]
    public async Task NestedGroup_681() {
        using Stream s = OpenTestFile("issues/681_nested.parquet");
        await using ParquetReader r = await ParquetReader.CreateAsync(s);
        using ParquetRowGroupReader rgr = r.OpenRowGroupReader(0);

        DataField df = r.Schema.FindDataField(new FieldPath("data_group", "nested"));

        string?[] values = await ReadStringValuesAsync(rgr, df);

        Assert.Single(values);
    }

    /// <summary>
    /// Implicit lists (repeated groups with no LIST annotation) with several fields, top level and inside a struct:
    ///   required int32 id;
    ///   repeated group addresses { required binary street (String); optional int32 zip; }
    ///   required group person { optional binary name (String); repeated group phones { required binary kind (String); optional binary number (String); } }
    /// Written by pyarrow 23.0.1 as required lists of required structs, then only the footer schema was rewritten to the
    /// implicit form (leaf columns have identical levels). pyarrow reads the result as list&lt;struct&gt;.
    /// Used to fail with "Implicit list with multiple children is currently not supported".
    /// </summary>
    [Fact]
    public async Task ImplicitList_MultipleChildren() {
        using Stream s = OpenTestFile("special/implicit_list_multiple_children.parquet");
        await using ParquetReader r = await ParquetReader.CreateAsync(s);
        using ParquetRowGroupReader rgr = r.OpenRowGroupReader(0);

        // schema: lists of structs, paths as written in the file
        ListField addresses = Assert.IsType<ListField>(r.Schema.Fields[1]);
        Assert.IsType<StructField>(addresses.Item);
        StructField person = Assert.IsType<StructField>(r.Schema.Fields[2]);
        ListField phones = Assert.IsType<ListField>(person.Fields[1]);
        Assert.IsType<StructField>(phones.Item);
        Assert.Equal(
            new[] { "id(0,0)", "addresses/street(1,1)", "addresses/zip(1,2)", "person/name(0,1)",
                "person/phones/kind(1,1)", "person/phones/number(1,2)" },
            r.Schema.DataFields.Select(f => $"{f.Path}({f.MaxRepetitionLevel},{f.MaxDefinitionLevel})"));

        // low level: values and levels, as e.g. a viewer reads them
        using(RawColumnData<int> zip = await rgr.ReadRawColumnDataAsync<int>(r.Schema.FindDataField(new FieldPath("addresses", "zip")))) {
            // Values is sized for every entry, only the defined ones (DL == max) are filled
            Assert.Equal(new[] { 10001, 20002, 30003, 40004 }, zip.Values[..4].ToArray());
            Assert.Equal(new[] { 2, 1, 0, 2, 2, 2, 1 }, zip.DefinitionLevels.ToArray());
            Assert.Equal(new[] { 0, 1, 0, 0, 0, 1, 1 }, zip.RepetitionLevels.ToArray());
        }
        using(RawColumnData<ReadOnlyMemory<char>> kind = await rgr.ReadRawColumnDataAsync<ReadOnlyMemory<char>>(
            r.Schema.FindDataField(new FieldPath("person", "phones", "kind")))) {
            Assert.Equal(new[] { "home", "work", "cell" }, kind.Values[..3].ToArray().Select(v => v.ToString()));
            Assert.Equal(new[] { 1, 1, 0, 1, 0 }, kind.DefinitionLevels.ToArray());
            Assert.Equal(new[] { 0, 1, 0, 0, 0 }, kind.RepetitionLevels.ToArray());
        }

        // untyped deserialization assembles the same rows pyarrow does
        s.Position = 0;
        IList<Dictionary<string, object>> rows = (await ParquetSerializer.DeserializeUntypedAsync(s)).Data!;
        Assert.Equal(4, rows.Count);
        var row1Addresses = Assert.IsAssignableFrom<IList>(rows[0]["addresses"]);
        Assert.Equal(2, row1Addresses.Count);
        Assert.Equal("2 Side Rd", ((IDictionary<string, object?>)row1Addresses[1]!)["street"]);
        Assert.Empty(Assert.IsAssignableFrom<IList>(rows[1]["addresses"]));
        Assert.Equal(3, Assert.IsAssignableFrom<IList>(rows[3]["addresses"]).Count);
        var row1Phones = Assert.IsAssignableFrom<IList>(((IDictionary<string, object?>)rows[0]["person"]!)["phones"]);
        Assert.Equal("work", ((IDictionary<string, object?>)row1Phones[1]!)["kind"]);
        Assert.Empty(Assert.IsAssignableFrom<IList>(((IDictionary<string, object?>)rows[3]["person"]!)["phones"]));
    }

    [Fact]
    public async Task BigDecimalDefaultOptions() {
        using Stream s = OpenTestFile("bigdecimal.parquet");
        await using ParquetReader r = await ParquetReader.CreateAsync(s);
        using ParquetRowGroupReader rgr = r.OpenRowGroupReader(0);

        DataField[] fields = r.Schema.GetDataFields();
        await Assert.ThrowsAsync<OverflowException>(async () => {
            foreach(DataField field in fields) {
                await ReadAnyFieldAsync(rgr, field);
            }
        });
    }

    [Fact]
    public async Task BigDecimalWithUseBigDecimalsOptionOn() {
        using Stream s = OpenTestFile("bigdecimal.parquet");
        await using ParquetReader r = await ParquetReader.CreateAsync(s, new ParquetOptions {
            UseBigDecimal = true
        });
        using ParquetRowGroupReader rgr = r.OpenRowGroupReader(0);

        foreach(DataField field in r.Schema.GetDataFields()) {
            await ReadAnyFieldAsync(rgr, field);
        }
    }

    // Dictionary-encoded INT96 column written by pyarrow 23.0.1, then two dictionary entries were patched in place:
    // one to julian day 5373485 (10000-01-01, after DateTime.MaxValue) and one to julian day 0 (4714 BC).
    private const string Int96OutOfRangeFile = "special/int96_out_of_range_dictionary.parquet";
    private static readonly DateTime Int96OkValue = new(2024, 5, 17, 8, 30, 15);

    [Fact]
    public async Task Int96_OutOfRange_ThrowsByDefault() {
        using Stream s = OpenTestFile(Int96OutOfRangeFile);
        await using ParquetReader r = await ParquetReader.CreateAsync(s);
        using ParquetRowGroupReader rgr = r.OpenRowGroupReader(0);

        ParquetException ex = await Assert.ThrowsAsync<ParquetException>(
            () => ReadNullableValuesAsync<DateTime>(rgr, r.Schema.DataFields[0]));
        Assert.Contains("'ts'", ex.Message);
        Assert.Contains("julian day 5373485", ex.Message);
        Assert.Contains(nameof(DateTimeOutOfRangeHandling.Clamp), ex.Message);
    }

    [Fact]
    public async Task Int96_OutOfRange_Clamp() {
        using Stream s = OpenTestFile(Int96OutOfRangeFile);
        await using ParquetReader r = await ParquetReader.CreateAsync(s, new ParquetOptions {
            DateTimeOutOfRangeHandling = DateTimeOutOfRangeHandling.Clamp
        });
        using ParquetRowGroupReader rgr = r.OpenRowGroupReader(0);

        DateTime?[] values = await ReadNullableValuesAsync<DateTime>(rgr, r.Schema.DataFields[0]);
        Assert.Equal(new DateTime?[] {
            Int96OkValue, DateTime.MaxValue, Int96OkValue, DateTime.MinValue, null, Int96OkValue
        }, values);
    }

    // Written by DuckDB v1.5.6 (plain encoding). Columns: id, d (DATE), ts (TIMESTAMP micros, not UTC adjusted).
    // Rows: 2024-05-17 [08:30], 12345-01-01, infinity, -infinity, 0000-06-15.
    // DuckDB stores infinity / -infinity as int32 (date) or int64 (timestamp) max / -max.
    private const string DuckDbOutOfRangeDatesFile = "special/duckdb_out_of_range_dates.parquet";

    [Theory]
    [InlineData("d", false, "DATE value 3789391")]
    [InlineData("d", true, "DATE value 3789391")]
    [InlineData("ts", false, "TIMESTAMP value 327403382400000000")]
    public async Task DateAndTimestamp_OutOfRange_ThrowsByDefault(string column, bool useDateOnly, string expectedValue) {
        using Stream s = OpenTestFile(DuckDbOutOfRangeDatesFile);
        await using ParquetReader r = await ParquetReader.CreateAsync(s, new ParquetOptions {
            UseDateOnlyTypeForDates = useDateOnly
        });
        using ParquetRowGroupReader rgr = r.OpenRowGroupReader(0);
        DataField field = r.Schema.DataFields.First(f => f.Name == column);

        ParquetException ex = await Assert.ThrowsAsync<ParquetException>(() => field.ClrType == typeof(DateOnly)
            ? ReadNullableValuesAsync<DateOnly>(rgr, field)
            : ReadNullableValuesAsync<DateTime>(rgr, field));
        Assert.Contains($"'{column}'", ex.Message);
        Assert.Contains(expectedValue, ex.Message);
        Assert.Contains(nameof(DateTimeOutOfRangeHandling.Clamp), ex.Message);
    }

    [Fact]
    public async Task DateAndTimestamp_OutOfRange_Clamp() {
        using Stream s = OpenTestFile(DuckDbOutOfRangeDatesFile);
        await using ParquetReader r = await ParquetReader.CreateAsync(s, new ParquetOptions {
            DateTimeOutOfRangeHandling = DateTimeOutOfRangeHandling.Clamp
        });
        using ParquetRowGroupReader rgr = r.OpenRowGroupReader(0);

        DateTime?[] dates = await ReadNullableValuesAsync<DateTime>(rgr, r.Schema.DataFields.First(f => f.Name == "d"));
        Assert.Equal(new DateTime?[] {
            new DateTime(2024, 5, 17), DateTime.MaxValue, DateTime.MaxValue, DateTime.MinValue, DateTime.MinValue
        }, dates);

        DateTime?[] timestamps = await ReadNullableValuesAsync<DateTime>(rgr, r.Schema.DataFields.First(f => f.Name == "ts"));
        Assert.Equal(new DateTime?[] {
            new DateTime(2024, 5, 17, 8, 30, 0), DateTime.MaxValue, DateTime.MaxValue, DateTime.MinValue, DateTime.MinValue
        }, timestamps);
        // kind is unchanged by clamping: this column is not UTC adjusted
        Assert.All(timestamps, t => Assert.Equal(DateTimeKind.Local, t!.Value.Kind));
    }

    [Fact]
    public async Task DateOnly_OutOfRange_Clamp() {
        using Stream s = OpenTestFile(DuckDbOutOfRangeDatesFile);
        await using ParquetReader r = await ParquetReader.CreateAsync(s, new ParquetOptions {
            UseDateOnlyTypeForDates = true,
            DateTimeOutOfRangeHandling = DateTimeOutOfRangeHandling.Clamp
        });
        using ParquetRowGroupReader rgr = r.OpenRowGroupReader(0);

        DateOnly?[] dates = await ReadNullableValuesAsync<DateOnly>(rgr, r.Schema.DataFields.First(f => f.Name == "d"));
        Assert.Equal(new DateOnly?[] {
            new DateOnly(2024, 5, 17), DateOnly.MaxValue, DateOnly.MaxValue, DateOnly.MinValue, DateOnly.MinValue
        }, dates);
    }

    [Fact]
    public async Task PyArrow22Async() {
        await using Stream s = OpenTestFile("special/pyarrow_v22.parquet");
        await using ParquetReader r = await ParquetReader.CreateAsync(s);

        using ParquetRowGroupReader groupReader = r.OpenRowGroupReader(0);

        Assert.Equal(4626, groupReader.RowCount);
        DataField[] fs = r.Schema.GetDataFields();
        Assert.Equal(2, fs.Length);

        long?[] data = await ReadNullableValuesAsync<long>(groupReader, fs[1]);
        Assert.Equal(21_572_000_000, data[0]);
    }

    /// <summary>
    /// Interesting in a way that dictionary-encoded columns have single dictionary page and several
    /// dictionary index pages. Dictionary page needs to be kept as is, but index pages need to feed from dictionary one
    /// after another.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task PyArrow23Async() {
        await using Stream s = OpenTestFile("special/pyarrow_v23.parquet");
        await using ParquetReader r = await ParquetReader.CreateAsync(s);

        DeserializationResult<Dictionary<string, object>> rs = await ParquetSerializer.DeserializeUntypedAsync(s);
        Assert.Equal(39_366, rs.Data.Count);
    }

    [Fact]
    public async Task PyArrow25() {
        // all_null_column_pyarrow_v25
        await using Stream s = OpenTestFile("special/all_null_column_pyarrow_v25.parquet");
        await using ParquetReader r = await ParquetReader.CreateAsync(s);

        DeserializationResult<Dictionary<string, object>> rs = await ParquetSerializer.DeserializeUntypedAsync(s);
    }


    [Fact]
    public async Task UnshreddedVariantAsync() {
        await using Stream s = OpenTestFile("variant_unshredded.parquet");
        await using ParquetReader r = await ParquetReader.CreateAsync(s);
        Assert.NotNull(r.Schema);
    }

}
