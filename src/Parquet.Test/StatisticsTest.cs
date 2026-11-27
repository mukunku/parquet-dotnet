using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Parquet.Data;
using Parquet.Schema;
using Xunit;

namespace Parquet.Test;

public class StatisticsTest : TestBase {

    [Fact]
    public async Task Int32_null_min_max() {
        var schema = new ParquetSchema(new DataField<int>("id"));
        var ms = new MemoryStream();
        await using(ParquetWriter w = await ParquetWriter.CreateAsync(schema, ms)) {
            using ParquetRowGroupWriter rg = w.CreateRowGroup();
            await rg.WriteAsync<int>(schema.DataFields[0], new int[] { 4, 2, 1, 3, 5, 1, 4 });
        }

        // read back
        ms.Position = 0;
        await using(ParquetReader r = await ParquetReader.CreateAsync(ms)) {
            using ParquetRowGroupReader rgr = r.OpenRowGroupReader(0);
            DataColumnStatistics? stats = rgr.GetStatistics(schema.DataFields[0]);
            Assert.NotNull(stats);
            Assert.Equal(0, stats.NullCount);
            Assert.Equal(1, stats.MinValue);
            Assert.Equal(5, stats.MaxValue);
        }
    }

    [Fact]
    public async Task Issue735() {
        ReadOnlyMemory<byte> id = new byte[]
        {
            1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28,
            29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54
        };

        IReadOnlyCollection<string> checkpoint =
        [
            "003510", "KZ0516", "KZ0527", "003543", "KZ0149", "003021", "KZ0105", "KZ0561", "CN0027", "CN0038",
            "CN0083", "CN0127", "CN0094", "KZ0716", "KZ0138", "KZ0483", "KZ0572", "KZ0249", "003632", "KZ0050",
            "KZ0683", "KZ0694", "KZ0494", "KZ0338", "KZ0449", "001374", "CN0161", "001330", "KZ0061", "KZ0327",
            "KZ0438", "KZ0016", "001307", "KZ0705", "KZ0261", "KZ0094", "KZ0738", "KZ0749", "FI0225", "FI0225",
            "FI0014", "001196", "FI0203", "FI0203", "PL0017", "PL0017", "PL0028", "PL0039", "001029", "FI0158",
            "FI0247", "KZ0372", "KZ0161", "KZ0172"
        ];

        IReadOnlyCollection<string> docId =
        [
            "0196", "0199", "0202", "0203", "0220", "0218", "0247", "0222", "0224", "0226", "0230", "0232", "0234",
            "0249", "0248", "0246", "0251", "0253", "0255", "0260", "0265", "0269", "0322", "0305", "0309", "0375",
            "0390", "0424", "0428", "0430", "0432", "0435", "0437", "0439", "0444", "0446", "0448", "0450", "0499",
            "0500", "0503", "0511", "0519", "0520", "0525", "0526", "0528", "0533", "0539", "0561", "0576", "0257",
            "0688", "0690"
        ];

        var fs = new MemoryStream();

        ParquetSchema ps = new
        (
            new DataField("ID", typeof(byte), false),
            new DataField("CHECKPOINT", typeof(string), false),
            new DataField("DOC_ID", typeof(string), false)
        );

        var parquetOptions = new ParquetOptions {
            UseBigDecimal = false,
            UseDateOnlyTypeForDates = true,
            MaximumSmallPoolFreeBytes = 32 * 1024 * 1024,
            MaximumLargePoolFreeBytes = 64 * 1024 * 1024,
            DictionaryEncodingThreshold = 0.5d
        };

        await using(ParquetWriter writer = await ParquetWriter.CreateAsync(ps, fs, parquetOptions)) {
            using ParquetRowGroupWriter groupWriter = writer.CreateRowGroup();

            await groupWriter.WriteAsync(ps.DataFields[0], id);
            await groupWriter.WriteAsync(ps.DataFields[1], checkpoint);
            await groupWriter.WriteAsync(ps.DataFields[2], docId);
            groupWriter.CompleteValidate();
        }

        fs.Position = 0L;
        await using ParquetReader reader = await ParquetReader.CreateAsync(fs);
        using ParquetRowGroupReader groupReader = reader.OpenRowGroupReader(0);
        DataColumnStatistics? dataColumnStatistics = groupReader.GetStatistics(reader.Schema.DataFields[1]);
        Assert.Equal(0, dataColumnStatistics?.NullCount);
    }

    // UUIDs whose min/max differ between the standard (big endian, RFC 4122) byte order and .NET's mixed endian
    // Guid.ToByteArray() layout. In UUID order the min is 00000001-... and the max ff000000-...
    private static readonly Guid[] UuidValues = [
        Guid.Parse("01000000-0000-0000-0000-0000000000ff"),
        Guid.Parse("00000001-0000-0000-0000-000000000000"),
        Guid.Parse("ff000000-0000-0000-0000-000000000000"),
        Guid.Parse("000000ff-ffff-ffff-ffff-ffffffffffff")];
    private static readonly Guid UuidMin = Guid.Parse("00000001-0000-0000-0000-000000000000");
    private static readonly Guid UuidMax = Guid.Parse("ff000000-0000-0000-0000-000000000000");

    /// <summary>
    /// UUID min/max statistics were written in .NET's mixed endian Guid byte order while the data is big endian, so
    /// other readers saw wrong ranges, e.g. a max smaller than half of the values, which lets query engines wrongly
    /// skip row groups (#758). They must be stored in the same byte order as the data, as pyarrow does.
    /// </summary>
    [Fact]
    public async Task Uuid_min_max_written_in_uuid_byte_order() {
        var field = new DataField<Guid>("id");
        var schema = new ParquetSchema(field);
        using var ms = new MemoryStream();
        await using(ParquetWriter w = await ParquetWriter.CreateAsync(schema, ms)) {
            using ParquetRowGroupWriter rg = w.CreateRowGroup();
            await rg.WriteAsync<Guid>(field, UuidValues);
        }

        ms.Position = 0;
        await using ParquetReader r = await ParquetReader.CreateAsync(ms);
        using ParquetRowGroupReader rgr = r.OpenRowGroupReader(0);
        Meta.Statistics stored = rgr.RowGroup.Columns[0].MetaData!.Statistics!;
        Assert.Equal(UuidMin.ToByteArray(bigEndian: true), stored.MinValue);
        Assert.Equal(UuidMax.ToByteArray(bigEndian: true), stored.MaxValue);

        DataColumnStatistics? stats = rgr.GetStatistics(r.Schema.DataFields[0]);
        Assert.Equal(UuidMin, stats?.MinValue);
        Assert.Equal(UuidMax, stats?.MaxValue);
    }

    /// <summary>
    /// INT64 timestamp min/max statistics were always written in milliseconds, while the data is written in the
    /// column's unit, so micros and nanos statistics were 1000x and 1000000x too small (#769).
    /// The extremes are 1999-12-31 23:59:59 UTC (946684799 s since 1970) and 2030-01-02 03:04:05 UTC (1893553445 s).
    /// </summary>
    [Theory]
    [InlineData(DateTimeTimeUnit.Millis, 1_000L)]
    [InlineData(DateTimeTimeUnit.Micros, 1_000_000L)]
    [InlineData(DateTimeTimeUnit.Nanos, 1_000_000_000L)]
    public async Task Timestamp_min_max_written_in_column_unit(DateTimeTimeUnit unit, long perSecond) {
        var field = new DateTimeDataField("ts", DateTimeFormat.Timestamp, unit: unit);
        var schema = new ParquetSchema(field);
        DateTime[] values = [
            new(2024, 5, 17, 8, 30, 15, DateTimeKind.Utc),
            new(1999, 12, 31, 23, 59, 59, DateTimeKind.Utc),
            new(2030, 1, 2, 3, 4, 5, DateTimeKind.Utc)];
        using var ms = new MemoryStream();
        await using(ParquetWriter w = await ParquetWriter.CreateAsync(schema, ms)) {
            using ParquetRowGroupWriter rg = w.CreateRowGroup();
            await rg.WriteAsync<DateTime>(field, values);
        }

        ms.Position = 0;
        await using ParquetReader r = await ParquetReader.CreateAsync(ms);
        using ParquetRowGroupReader rgr = r.OpenRowGroupReader(0);
        Meta.Statistics stored = rgr.RowGroup.Columns[0].MetaData!.Statistics!;
        Assert.Equal(946_684_799L * perSecond, BitConverter.ToInt64(stored.MinValue!));
        Assert.Equal(1_893_553_445L * perSecond, BitConverter.ToInt64(stored.MaxValue!));
    }

    /// <summary>
    /// UUID statistics written by pyarrow 23.0.1 (the same values as above) read back as Guids. They used to be
    /// returned as null, as fixed length byte array statistics other than decimals were not decoded.
    /// </summary>
    [Fact]
    public async Task Uuid_min_max_read_from_pyarrow() {
        await using ParquetReader r = await ParquetReader.CreateAsync(OpenTestFile("special/uuid_statistics_pyarrow.parquet"));
        using ParquetRowGroupReader rgr = r.OpenRowGroupReader(0);

        DataColumnStatistics? stats = rgr.GetStatistics(r.Schema.DataFields[0]);
        Assert.Equal(UuidMin, stats?.MinValue);
        Assert.Equal(UuidMax, stats?.MaxValue);
    }
}