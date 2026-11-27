using System;
using System.Linq;
using System.Threading.Tasks;
using Parquet.Data;
using Parquet.Schema;
using Xunit;

namespace Parquet.Test;

public class ParquetRowGroupReaderTest : TestBase {

    [Theory]
    [InlineData("multi.page.parquet")]
    [InlineData("multi.page.v2.parquet")]
    public async Task GetColumnStatistics_ShouldNotBeEmpty(string parquetFile) {
        await using(ParquetReader reader = await ParquetReader.CreateAsync(OpenTestFile(parquetFile), leaveStreamOpen: false)) {
            for(int gidx = 0; gidx < reader.RowGroupCount; gidx++) {
                using(ParquetRowGroupReader rowGroupReader = reader.OpenRowGroupReader(0)) {

                    foreach(DataField df in reader.Schema.DataFields) {
                        DataColumnStatistics? stats = rowGroupReader.GetStatistics(df);

                        Assert.NotNull(stats);
                    }
                }
            }
        }

    }

    [Theory]
    [InlineData("multi.page.parquet")]
    [InlineData("multi.page.v2.parquet")]
    public async Task GetColumnReader_MustFailOnInvalidField(string parquetFile) {
        await using(ParquetReader reader = await ParquetReader.CreateAsync(OpenTestFile(parquetFile), leaveStreamOpen: false)) {
            using(ParquetRowGroupReader rowGroupReader = reader.OpenRowGroupReader(0)) {

                Assert.Throws<ArgumentNullException>(() => rowGroupReader.GetStatistics(null!));
                DataField nonExistingField = new DataField("non_existing_field7862425", typeof(int));
                Assert.Throws<ParquetException>(() => rowGroupReader.GetStatistics(nonExistingField));
            }
        }
    }

    /// <summary>
    /// ReadAsync into nullable memory rents a definition level buffer from the shared pool. Required columns never
    /// write definition levels, so leftovers from an earlier use of the pool (negative numbers here) used to be taken
    /// for nulls, returning random nulls in a column that cannot contain any and shifting the following values.
    /// </summary>
    [Fact]
    public async Task ReadAsync_Nullable_RequiredColumn_IgnoresPooledGarbage() {
        const int count = 10_000;
        var field = new DataField<int>("id");   // int, not int?: required column
        var schema = new ParquetSchema(field);
        using var ms = new System.IO.MemoryStream();
        await using(ParquetWriter writer = await ParquetWriter.CreateAsync(schema, ms)) {
            using ParquetRowGroupWriter rgw = writer.CreateRowGroup();
            await rgw.WriteAsync<int>(field, System.Linq.Enumerable.Range(0, count).ToArray());
        }

        // leave junk in the shared pool, like an earlier read would
        using(var junk = CommunityToolkit.HighPerformance.Buffers.MemoryOwner<int>.Allocate(count)) {
            junk.Span.Fill(-7);
        }

        ms.Position = 0;
        await using ParquetReader reader = await ParquetReader.CreateAsync(ms);
        using ParquetRowGroupReader rgr = reader.OpenRowGroupReader(0);
        Assert.Equal(0, reader.Schema.DataFields[0].MaxDefinitionLevel);

        var values = new int?[count];
        await rgr.ReadAsync<int>(reader.Schema.DataFields[0], values.AsMemory());

        Assert.Equal(System.Linq.Enumerable.Range(0, count).Select(i => (int?)i), values);
    }

}




