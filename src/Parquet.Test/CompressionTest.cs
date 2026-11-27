using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Parquet.Data;
using Parquet.Schema;
using Xunit;

namespace Parquet.Test;

// CompressionMethod.LZ4 is obsolete for users, but still written when asked for and read
#pragma warning disable CS0618

public class CompressionTest : TestBase {
    [Theory]
    [InlineData(CompressionMethod.None)]
    [InlineData(CompressionMethod.Snappy)]
    [InlineData(CompressionMethod.Gzip)]
    //[InlineData(CompressionMethod.Lzo)]
    [InlineData(CompressionMethod.Brotli)]
    [InlineData(CompressionMethod.LZ4)]
    [InlineData(CompressionMethod.Zstd)]
    [InlineData(CompressionMethod.Lz4Raw)]
    public async Task Compression_roundtrip(CompressionMethod compressionMethod) {
        /*
         * uncompressed: length - 14, levels - 6
         * 
         * 
         */

        using var ms = new MemoryStream();
        var schema = new ParquetSchema(new DataField<int>("id"));

        int testValue = 10;

        // write value
        await using(ParquetWriter w = await ParquetWriter.CreateAsync(schema, ms, new ParquetOptions { CompressionMethod = compressionMethod })) {
            using ParquetRowGroupWriter rgw = w.CreateRowGroup();
            await rgw.WriteAsync<int>(schema.DataFields[0], new[] { testValue });
        }

        // read value back
        ms.Position = 0;
        await using(ParquetReader r = await ParquetReader.CreateAsync(ms)) {
            RawColumnData<int> c = await ReadColumn<int>(r, schema.DataFields[0]);
            Assert.Equal(testValue, c.Values[0]);
        }
    }

    [Fact]
    public async Task Zstd_decompression_succeeds_when_parquet_metadata_has_incorrect_uncompressed_page_size() {
        // This file has a Zstd-compressed column whose UncompressedPageSize in the Parquet metadata
        // is incorrect (8 instead of the actual 1024 bytes). The library must ignore the metadata hint
        // and rely solely on the size embedded in the Zstd frame header.
        await using ParquetReader reader = await ParquetReader.CreateAsync(OpenTestFile("special/zstd-invalid-length.parquet"));
        using ParquetRowGroupReader groupReader = reader.OpenRowGroupReader(0);

        DataField[] fields = reader.Schema.GetDataFields();
        RawColumnData<DateTime> column1 = await ReadColumn<DateTime>(reader, fields[0]);
        RawColumnData<ReadOnlyMemory<char>> column2 = await ReadColumn<ReadOnlyMemory<char>>(reader, fields[1]);

        Assert.True(column1.Values.Length > 0);
        Assert.True(column2.Values.Length > 0);
    }

    [Fact]
    public async Task DataPageV2_gzip_required_column_with_definition_levels() {
        // Data page V2 with GZIP, where the page carries definition levels although the column is REQUIRED.
        // Levels must be skipped using the page header byte lengths, otherwise decompression starts inside the
        // levels and fails with "The archive entry was compressed using an unsupported compression method".
        // Made by writing a nullable (no nulls) int64 column with pyarrow 23.0.1, data_page_version="2.0",
        // compression="gzip", then rewriting only the footer to mark the column REQUIRED and drop size statistics.
        // pyarrow reads the resulting file correctly.
        await using ParquetReader reader = await ParquetReader.CreateAsync(OpenTestFile("special/v2_gzip_required_with_def_levels.parquet"));

        DataField field = Assert.Single(reader.Schema.GetDataFields());
        Assert.Equal(0, field.MaxDefinitionLevel);

        RawColumnData<long> column = await ReadColumn<long>(reader, field);
        Assert.Equal(Enumerable.Range(0, 1000).Select(i => (long)i), column.Values.ToArray());
    }

    /// <summary>
    /// Reference files from https://github.com/apache/parquet-testing/tree/master/data, all with the same 4 rows.
    /// The legacy LZ4 codec is ambiguous: parquet-mr (Spark, Hive...) writes Hadoop framed blocks, parquet-cpp a raw
    /// block. Hadoop framed files used to fail with garbage data ("page data ends after 0 of 2 values").
    /// </summary>
    [Theory]
    [InlineData("hadoop_lz4_compressed.parquet", CompressionMethod.LZ4)]         // parquet-mr 1.10.1, Hadoop framing
    [InlineData("non_hadoop_lz4_compressed.parquet", CompressionMethod.LZ4)]     // parquet-cpp, raw block
    [InlineData("lz4_raw_compressed.parquet", CompressionMethod.Lz4Raw)]         // parquet-cpp
    public async Task Lz4_reference_files(string fileName, CompressionMethod expectedCodec) {
        await using ParquetReader reader = await ParquetReader.CreateAsync(OpenTestFile(fileName));
        using ParquetRowGroupReader rgr = reader.OpenRowGroupReader(0);
        Assert.Equal(expectedCodec, (CompressionMethod)(int)rgr.RowGroup.Columns[0].MetaData!.Codec);

        DataField[] fields = reader.Schema.GetDataFields();
        long?[] c0 = await ReadNullableValuesAsync<long>(rgr, fields[0]);
        byte[][] c1 = await ReadBinaryValuesAsync(rgr, fields[1]);
        double?[] v11 = await ReadNullableValuesAsync<double>(rgr, fields[2]);

        Assert.Equal(new long?[] { 1593604800, 1593604800, 1593604801, 1593604801 }, c0);
        Assert.Equal(new[] { "abc", "def", "abc", "def" }, c1.Select(b => System.Text.Encoding.UTF8.GetString(b)));
        Assert.Equal(new double?[] { 42.0, 7.7, 42.125, 7.7 }, v11);
    }

    [Fact]
    public async Task Lz4_hadoop_framed_larger_reference_file() {
        // parquet-mr 1.11.1 (avro), 10000 distinct UUID strings, Hadoop framed LZ4
        await using ParquetReader reader = await ParquetReader.CreateAsync(OpenTestFile("hadoop_lz4_compressed_larger.parquet"));
        using ParquetRowGroupReader rgr = reader.OpenRowGroupReader(0);

        string?[] a = await ReadStringValuesAsync(rgr, Assert.Single(reader.Schema.GetDataFields()));
        Assert.Equal(10000, a.Length);
        Assert.Equal("c7ce6bef-d5b0-4863-b199-8ea8c7fb117b", a[0]);
        Assert.Equal("85440778-460a-41ac-aa2e-ac3ee41696bf", a[9999]);
        Assert.Equal(10000, a.Distinct().Count());
    }
}