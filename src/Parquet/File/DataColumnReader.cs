using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.HighPerformance.Buffers;
using Microsoft.IO;
using Parquet.Data;
using Parquet.Encodings;
using Parquet.Extensions;
using Parquet.Meta;
using Parquet.Meta.Proto;
using Parquet.Schema;

namespace Parquet.File;

/// <summary>
/// Reader for Parquet data column
/// </summary>
class DataColumnReader {
    private readonly DataField _dataField;
    private readonly Stream _inputStream;
    private readonly ColumnChunk _thriftColumnChunk;
    private readonly SchemaElement? _schemaElement;
    private readonly ThriftFooter _footer;
    private readonly ParquetOptions _options;
    private readonly DataColumnStatistics? _stats;
    private readonly CompressionMethod _compressionMethod;
    private static readonly RecyclableMemoryStreamManager _rmsMgr = new RecyclableMemoryStreamManager();

    internal DataColumnReader(
       DataField dataField,
       Stream inputStream,
       ColumnChunk thriftColumnChunk,
       DataColumnStatistics? stats,
       ThriftFooter footer,
       ParquetOptions? parquetOptions) {
        _dataField = dataField ?? throw new ArgumentNullException(nameof(dataField));
        _inputStream = inputStream ?? throw new ArgumentNullException(nameof(inputStream));
        _thriftColumnChunk = thriftColumnChunk ?? throw new ArgumentNullException(nameof(thriftColumnChunk));
        _stats = stats;
        _compressionMethod = (CompressionMethod)(int)(thriftColumnChunk.MetaData?.Codec ?? CompressionCodec.UNCOMPRESSED);
        _footer = footer ?? throw new ArgumentNullException(nameof(footer));
        _options = parquetOptions ?? throw new ArgumentNullException(nameof(parquetOptions));

        dataField.EnsureAttachedToSchema(nameof(dataField));

        _schemaElement = _footer.GetSchemaElement(_thriftColumnChunk);

        // parquetOptions is guaranteed non-null due to earlier null check.
        _rmsMgr.Settings.MaximumSmallPoolFreeBytes = parquetOptions.MaximumSmallPoolFreeBytes;
        _rmsMgr.Settings.MaximumLargePoolFreeBytes = parquetOptions.MaximumLargePoolFreeBytes;
    }

    /// <summary>
    /// Return data column statistics
    /// </summary>
    /// <returns>Data column statistics or null</returns>
    public DataColumnStatistics? GetColumnStatistics() => _stats;

    public async ValueTask ReadAsync<T>(ReadingColumn<T> rc, CancellationToken cancellationToken) where T : struct {
        // how many values are in column chunk, as there may be multiple data pages
        int totalValuesInChunk = (int)_thriftColumnChunk.MetaData!.NumValues;
        int definedValuesCount = totalValuesInChunk;
        if(_stats?.NullCount != null)
            definedValuesCount -= (int)_stats.NullCount.Value;

        long fileOffset = GetFileOffset();
        long pageOffset = fileOffset;

        while(rc.ValuesRead < totalValuesInChunk) {
            // use absolute positioning on every page read, because in some edge cases page reader may not exhaust or over-read page data
            _inputStream.Seek(pageOffset, SeekOrigin.Begin);
            PageHeader ph = PageHeader.Read(new ThriftCompactProtocolReader(_inputStream));
            pageOffset = _inputStream.Position + ph.CompressedPageSize;

            switch(ph.Type) {
                case PageType.DICTIONARY_PAGE:
                    await ReadDictionaryPageAsync(ph, rc, cancellationToken);
                    break;
                case PageType.DATA_PAGE:
                    await ReadDataPageV1Async(ph, rc, cancellationToken);
                    break;
                case PageType.DATA_PAGE_V2:
                    await ReadDataPageV2Async(ph, rc, totalValuesInChunk, cancellationToken);
                    break;
                default:
                    throw new NotSupportedException($"can't read page type {ph.Type}");
            }
        }
    }

    private async ValueTask<IMemoryOwner<byte>> ReadPageDataAsync(PageHeader ph) {
        Stream src = _inputStream.Sub(_inputStream.Position, ph.CompressedPageSize);
        return await Compressor.Instance.Decompress(_compressionMethod, src, ph.UncompressedPageSize);
    }

    private async ValueTask ReadDictionaryPageAsync<T>(PageHeader ph, ReadingColumn<T> rc, CancellationToken cancellationToken) where T : struct {

        if(rc.HasDictionary)
            throw new InvalidOperationException("dictionary already read");

        //Dictionary page format: the entries in the dictionary - in dictionary order - using the plain encoding.
        using IMemoryOwner<byte> bytes = await ReadPageDataAsync(ph);

        // Dictionary should not contain null values
        Span<T> dictionary = rc.AllocateDictionary(ph.DictionaryPageHeader!.NumValues);

        ParquetPlainEncoder.Decode(dictionary, ph.DictionaryPageHeader.NumValues,
               _schemaElement!, bytes.Memory.Span, out int dictionaryOffset, _options);
    }

    private long GetFileOffset() =>
        // get the minimum offset, we'll just read pages in sequence as DictionaryPageOffset/Data_page_offset are not reliable
        new[]
            {
                _thriftColumnChunk.MetaData?.DictionaryPageOffset ?? 0,
                _thriftColumnChunk.MetaData!.DataPageOffset,
                _thriftColumnChunk.MetaData?.IndexPageOffset ?? 0
            }
            .Where(e => e != 0)
            .Min();

    private async ValueTask ReadDataPageV1Async<T>(PageHeader ph, ReadingColumn<T> rc, CancellationToken cancellationToken) where T : struct {
        using IMemoryOwner<byte> bytes = await ReadPageDataAsync(ph);

        if(ph.DataPageHeader == null) {
            throw new ParquetException($"column '{_dataField.Path}' is missing data page header, file is corrupt");
        }

        int dataUsed = 0;
        int allValueCount = (int)_thriftColumnChunk.MetaData!.NumValues;
        int pageValueCount = ph.DataPageHeader.NumValues;

        if(_dataField.MaxRepetitionLevel > 0) {
            int levelsRead = ReadLevels(
                bytes.Memory.Span, _dataField.MaxRepetitionLevel,
                rc.RepetitionLevelsToReadInto,
                pageValueCount, null, out int usedLength);
            rc.MarkRepetitionLevels(levelsRead);
            dataUsed += usedLength;
        }

        int defNulls = 0;
        if(_dataField.MaxDefinitionLevel > 0) {
            int levelsRead = ReadLevels(
                bytes.Memory.Span.Slice(dataUsed), _dataField.MaxDefinitionLevel,
                rc.DefinitionLevelsToReadInto,
                pageValueCount, null, out int usedLength);
            dataUsed += usedLength;
            defNulls = rc.MarkDefinitionLevels(levelsRead, _dataField.MaxDefinitionLevel);
        }

        // try to be clever to detect how many elements to read
        int dataElementCount = pageValueCount - defNulls;

        ReadColumn(
            bytes.Memory.Span.Slice(dataUsed),
            ph.DataPageHeader.Encoding,
            allValueCount, dataElementCount,
            rc);
    }

    private async ValueTask ReadDataPageV2Async<T>(PageHeader ph, ReadingColumn<T> rc, long maxValues, CancellationToken cancellationToken) where T : struct {
        if(ph.DataPageHeaderV2 == null) {
            throw new ParquetException($"column '{_dataField.Path}' is missing data page header, file is corrupt");
        }

        using var pageMemory = MemoryOwner<byte>.Allocate(ph.CompressedPageSize);
        await using(Stream src = _inputStream.Sub(_inputStream.Position, ph.CompressedPageSize)) {
            await src.CopyToAsync(pageMemory.Memory, cancellationToken);
        }
        // V2 levels are stored uncompressed in front of the values and their sizes are always given in the header.
        // Skip them by those sizes, not by what the schema implies: some writers emit levels for required columns.
        int rlLength = ph.DataPageHeaderV2.RepetitionLevelsByteLength;
        int dlLength = ph.DataPageHeaderV2.DefinitionLevelsByteLength;
        int dataUsed = rlLength + dlLength;

        if(_dataField.MaxRepetitionLevel > 0) {
            int levelsRead = ReadLevels(pageMemory.Span,
                _dataField.MaxRepetitionLevel, rc.RepetitionLevelsToReadInto,
                ph.DataPageHeaderV2.NumValues, rlLength, out _);
            rc.MarkRepetitionLevels(levelsRead);

            throw new NotImplementedException();
        }

        if(_dataField.MaxDefinitionLevel > 0) {
            int levelsRead = ReadLevels(
                pageMemory.Memory.Span.Slice(rlLength), _dataField.MaxDefinitionLevel,
                rc.DefinitionLevelsToReadInto,
                ph.DataPageHeaderV2.NumValues, dlLength, out _);
            rc.MarkDefinitionLevels(levelsRead, _dataField.MaxDefinitionLevel);
        }

        int maxReadCount = ph.DataPageHeaderV2.NumValues - ph.DataPageHeaderV2.NumNulls;

        if(ph.DataPageHeaderV2.IsCompressed == false || _thriftColumnChunk.MetaData!.Codec == CompressionCodec.UNCOMPRESSED) {
            ReadColumn(pageMemory.Span.Slice(dataUsed), ph.DataPageHeaderV2.Encoding, maxValues, maxReadCount, rc);
            return;
        }

        int dataSize = ph.CompressedPageSize - ph.DataPageHeaderV2.RepetitionLevelsByteLength -
                       ph.DataPageHeaderV2.DefinitionLevelsByteLength;

        int decompressedSize = ph.UncompressedPageSize - ph.DataPageHeaderV2.RepetitionLevelsByteLength -
                               ph.DataPageHeaderV2.DefinitionLevelsByteLength;


        // decompress into rented memory
        using IMemoryOwner<byte> decompressedDataMemory = await Compressor.Instance.Decompress(
            _compressionMethod,
            pageMemory.Memory.Sub(dataUsed, pageMemory.Length - dataUsed),
            ph.UncompressedPageSize);

        ReadColumn(decompressedDataMemory.Memory.Span,
            ph.DataPageHeaderV2.Encoding,
            maxValues, maxReadCount,
            rc);
    }

    private int ReadLevels(Span<byte> s, int maxLevel,
        Span<int> dest,
        int pageSize,
        int? length, out int usedLength) {

        int bitWidth = maxLevel.GetBitWidth();

        return RleBitpackedHybridEncoder.Decode(s, bitWidth, length, out usedLength, dest, pageSize);
    }

    private void ReadColumn<T>(Span<byte> src,
        Encoding encoding, long totalValuesInChunk, int totalValuesInPage,
        ReadingColumn<T> rc)
        where T : struct {
        //dictionary encoding uses RLE to encode data

        switch(encoding) {
            case Encoding.PLAIN: { // 0
                    ParquetPlainEncoder.Decode(rc.ValuesToReadInto,
                        totalValuesInPage,
                        _schemaElement!, src, out int read, _options);
                    rc.MarkValuesRead(read);
                }
                break;

            case Encoding.PLAIN_DICTIONARY: // 2  // values are still encoded in RLE
            case Encoding.RLE_DICTIONARY: { // 8
                    Span<int> span = rc.AllocateOrGetDictionaryIndexes(totalValuesInPage);
                    int indexCount = ReadRleDictionary(src, totalValuesInPage, span);
                    rc.MarkDictionaryIndexesRead(indexCount);
                    rc.Checkpoint();
                }
                break;

            case Encoding.RLE: { // 3
                    if(_dataField.ClrType == typeof(bool)) {
                        // for boolean values, we need to read into temporary int buffer and convert to booleans.
                        // todo: we can optimise this by implementing boolean RLE decoder
                        Span<bool> dest = rc.ValuesToReadInto.AsSpan<T, bool>();

                        int[] tmp = new int[dest.Length];
                        int read = RleBitpackedHybridEncoder.Decode(src,
                            _schemaElement!.TypeLength ?? 0,
                            src.Length, out int usedLength, tmp.AsSpan(), totalValuesInPage);

                        // copy back to bool array
                        for(int i = 0; i < read; i++) {
                            dest[i] = tmp[i] == 1;
                        }

                        rc.MarkValuesRead(read);
                    } else {
                        Span<int> dest = rc.ValuesToReadInto.AsSpan<T, int>();
                        int read = RleBitpackedHybridEncoder.Decode(src,
                            _schemaElement!.TypeLength ?? 0,
                            src.Length, out int usedLength, dest, totalValuesInPage);
                        rc.MarkValuesRead(read);
                    }
                }
                break;

            case Encoding.DELTA_BINARY_PACKED: {// 5
                    int read = DeltaBinaryPackedEncoder.IsSupported(typeof(T))
                        ? DeltaBinaryPackedEncoder.Decode(src, rc.ValuesToReadInto, totalValuesInPage, out _)
                        : DecodeDeltaBinaryPackedViaPlain(src, rc.ValuesToReadInto, totalValuesInPage);
                    rc.MarkValuesRead(read);
                }
                break;

            case Encoding.DELTA_LENGTH_BYTE_ARRAY: {  // 6
                    int read = DeltaLengthByteArrayEncoder.Decode(src, rc.ValuesToReadInto, totalValuesInPage);
                    rc.MarkValuesRead(read);
                }
                break;

            case Encoding.DELTA_BYTE_ARRAY: {         // 7
                    int read = DeltaByteArrayEncoder.Decode(src, rc.ValuesToReadInto, totalValuesInPage);
                    rc.MarkValuesRead(read);
                }
                break;

            case Encoding.BYTE_STREAM_SPLIT: {       // 9
                    if(ByteStreamSplitEncoder.CanDecodeDirectly<T>()) {
                        ByteStreamSplitEncoder.Decode(src, rc.ValuesToReadInto.Slice(0, totalValuesInPage));
                        rc.MarkValuesRead(totalValuesInPage);
                    } else {
                        // types converted from the physical value (dates, timestamps, decimals, fixed length byte
                        // arrays): un-split into plain bytes and let the plain decoder do the conversion
                        int width = _schemaElement!.Type switch {
                            Meta.Type.INT32 or Meta.Type.FLOAT => 4,
                            Meta.Type.INT64 or Meta.Type.DOUBLE => 8,
                            Meta.Type.FIXED_LEN_BYTE_ARRAY => _schemaElement.TypeLength
                                ?? throw new ParquetException($"column '{_dataField.Path}' is missing type length"),
                            _ => throw new ParquetException(
                                $"encoding {encoding} is not valid for physical type {_schemaElement.Type} in column '{_dataField.Path}'")
                        };
                        int plainLength = totalValuesInPage * width;
                        byte[] plain = ArrayPool<byte>.Shared.Rent(plainLength);
                        try {
                            ByteStreamSplitEncoder.DecodeToPlain(src, plain.AsSpan(0, plainLength), width, totalValuesInPage);
                            ParquetPlainEncoder.Decode(rc.ValuesToReadInto, totalValuesInPage,
                                _schemaElement, plain.AsSpan(0, plainLength), out int read, _options);
                            rc.MarkValuesRead(read);
                        } finally {
                            ArrayPool<byte>.Shared.Return(plain);
                        }
                    }
                }
                break;
            case Encoding.BIT_PACKED:                // 4 (deprecated)
            default:
                throw new ParquetException($"encoding {encoding} is not supported.");
        }
    }

    /// <summary>
    /// Handles DELTA_BINARY_PACKED columns whose CLR type is a logical type over INT32/INT64
    /// (DateTime, DateOnly, TimeOnly, TimeSpan, decimal, etc.). Values are delta-decoded into their
    /// physical representation, then reinterpreted as PLAIN bytes so the plain decoder applies the
    /// exact same logical-type conversion it uses for PLAIN-encoded pages.
    /// </summary>
    private int DecodeDeltaBinaryPackedViaPlain<T>(Span<byte> src, Span<T> dest, int totalValuesInPage)
        where T : struct {
        switch(_schemaElement!.Type) {
            case Meta.Type.INT32: {
                    int[] buf = ArrayPool<int>.Shared.Rent(totalValuesInPage);
                    try {
                        int n = DeltaBinaryPackedEncoder.Decode(src, buf.AsSpan(), totalValuesInPage, out _);
                        ParquetPlainEncoder.Decode(dest, n, _schemaElement,
                            MemoryMarshal.AsBytes(buf.AsSpan(0, n)), out int read);
                        return read;
                    } finally {
                        ArrayPool<int>.Shared.Return(buf);
                    }
                }
            case Meta.Type.INT64: {
                    long[] buf = ArrayPool<long>.Shared.Rent(totalValuesInPage);
                    try {
                        int n = DeltaBinaryPackedEncoder.Decode(src, buf.AsSpan(), totalValuesInPage, out _);
                        ParquetPlainEncoder.Decode(dest, n, _schemaElement,
                            MemoryMarshal.AsBytes(buf.AsSpan(0, n)), out int read);
                        return read;
                    } finally {
                        ArrayPool<long>.Shared.Return(buf);
                    }
                }
            default:
                throw new ParquetException(
                    $"{Encoding.DELTA_BINARY_PACKED} is only valid for {Meta.Type.INT32} and {Meta.Type.INT64}, but column '{_dataField.Path}' has physical type {_schemaElement.Type}");
        }
    }

    private static int ReadRleDictionary(Span<byte> s, int maxReadCount, Span<int> dest) {
        int offset = 0;
        int destOffset = 0;
        int start = destOffset;
        int bitWidth = s[offset++];

        int length = s.Length - 1;

        //when bit width is zero reader must stop and just repeat zero maxValue number of times
        if(bitWidth == 0 || length == 0) {
            for(int i = 0; i < maxReadCount; i++) {
                dest[destOffset++] = 0;
            }
        } else {
            if(length != 0) {
                destOffset += RleBitpackedHybridEncoder.Decode(s.Slice(1), bitWidth, length, out int usedLength, dest, maxReadCount);
            }
        }

        return destOffset - start;
    }
}
