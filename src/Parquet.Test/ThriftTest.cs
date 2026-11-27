using System.IO;
using System.Threading.Tasks;
using Parquet.Meta;
using Parquet.Meta.Proto;
using Xunit;

namespace Parquet.Test;

public class ThriftTest : TestBase {
    [Fact]
    public void TestFileRead_Table() {
        using Stream fs = OpenTestFile("thrift/wide.bin");
        FileMetaData fileMeta = FileMetaData.Read(new ThriftCompactProtocolReader(fs));
    }

    /// <summary>
    /// Fields unknown to the reader (e.g. added by a newer format version) must be skipped exactly, otherwise the
    /// fields after them are misread (#640, likely #800). Skipping a struct used to read its field headers without
    /// skipping their values, and sets and maps could not be skipped at all (#544).
    /// Hand encoded KeyValue struct (Thrift compact protocol): known field 1, unknown fields 3..11 of every type,
    /// then known field 2.
    /// </summary>
    [Fact]
    public void UnknownFields_AreSkipped() {
        byte[] data = [
            0x18, 0x01, (byte)'a',                      // 1: Key = "a" (binary)
            0x2C,                                       // 3: unknown struct {
                0x15, 0x0E,                             //    1: i32 7
                0x18, 0x02, (byte)'x', (byte)'y',       //    2: binary "xy"
                0x1C, 0x11, 0x00,                       //    3: struct { 1: bool true }
                0x19, 0x21, 0x01, 0x02,                 //    4: list<bool> [true, false], a byte per element
                0x00,                                   // }
            0x1A, 0x35, 0x02, 0x04, 0x06,               // 4: unknown set<i32> {1, 2, 3}
            0x1B, 0x02, 0x85,                           // 5: unknown map<binary, i32>, 2 pairs
                0x01, (byte)'k', 0x02,                  //    "k" -> 1
                0x01, (byte)'m', 0x04,                  //    "m" -> 2
            0x1B, 0x00,                                 // 6: unknown empty map (no type byte)
            0x19, 0x31, 0x01, 0x02, 0x01,               // 7: unknown list<bool> [true, false, true]
            0x1D, .. new byte[16],                      // 8: unknown uuid
            0x17, .. new byte[8],                       // 9: unknown double
            0x13, 0x7F,                                 // 10: unknown byte
            0x16, 0xD8, 0x04,                           // 11: unknown i64 300
            0x08, 0x04, 0x01, (byte)'b',                // 2: Value = "b" (field id 2 < 11, so long form header)
            0x00                                        // stop
        ];

        KeyValue kv = KeyValue.Read(new ThriftCompactProtocolReader(new MemoryStream(data)));

        Assert.Equal("a", kv.Key);
        Assert.Equal("b", kv.Value);
    }

    /// <summary>
    /// Stream.ReadByte returns -1 at the end, which as a byte (0xFF) always continues a varint, so truncated metadata
    /// (e.g. from an interrupted write, #723) used to loop forever. It must fail instead.
    /// </summary>
    [Fact]
    public void TruncatedData_Throws() {
        byte[] data = [0x18, 0x81];     // 1: Key, binary whose length varint is cut off mid-way
        Assert.Throws<ThriftProtocolException>(() => KeyValue.Read(new ThriftCompactProtocolReader(new MemoryStream(data))));
    }

    /*
     * Files written by pyarrow 23.0.1 (id int32: 1, 2, 3, 4; name string: a, b, null, d), then hand-encoded unknown
     * fields were spliced into their metadata: a struct (holding an i32, a binary, a nested struct and a list<bool>),
     * a set<i32>, a map<binary, i32> and a list<bool>, with field ids 100-103.
     * - thrift_unknown_footer_fields: in FileMetaData, between field 1 (version) and field 2 (schema).
     * - thrift_unknown_page_header_fields: at the end of the first column's data page header; the page and every
     *   offset after it were moved along and the column/row group sizes updated.
     * pyarrow reads both exactly like the original file. The old reader failed on both ("unexpected data type Set").
     */
    [Theory]
    [InlineData("special/thrift_unknown_footer_fields.parquet")]
    [InlineData("special/thrift_unknown_page_header_fields.parquet")]
    public async Task UnknownFieldsInFile_AreSkipped(string fileName) {
        await using ParquetReader reader = await ParquetReader.CreateAsync(OpenTestFile(fileName));
        using ParquetRowGroupReader rgr = reader.OpenRowGroupReader(0);

        Assert.Equal(new int?[] { 1, 2, 3, 4 }, await ReadNullableValuesAsync<int>(rgr, reader.Schema.DataFields[0]));
        Assert.Equal(new string?[] { "a", "b", null, "d" }, await ReadStringValuesAsync(rgr, reader.Schema.DataFields[1]));
    }

    /// <summary>
    /// The same pyarrow file with a footer that ends mid-varint: the known first field, then an unknown i64 field whose
    /// value stops after a continuation byte, as an interrupted write could leave it. The old reader hung forever on
    /// it; pyarrow reports "No more data to read". The timeout turns a regression into a failure instead of a hang.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task TruncatedFooter_Throws() {
        await Assert.ThrowsAsync<ThriftProtocolException>(
            async () => await ParquetReader.CreateAsync(OpenTestFile("special/thrift_truncated_footer.parquet")));
    }
}