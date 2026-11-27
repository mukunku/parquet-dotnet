using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using CommunityToolkit.HighPerformance.Buffers;
using K4os.Compression.LZ4;
using Parquet.Extensions;
using Snappier;

namespace Parquet.File;

/// <summary>
/// Data compression interface. Unfortunately it's practically impossible to perform streaming in parquet pages. When
/// this interface is stable for some time, it will allow public exposure and "Bring Your Own Compressor" plugins, but
/// not right now.
/// </summary>
interface ICompressor {

    /// <summary>
    /// Compresses data from the source stream using the specified compression method and level. Returns an
    /// <see cref="IMemoryOwner{T}"/> containing the compressed data.
    /// </summary>
    ValueTask<IMemoryOwner<byte>> CompressAsync(CompressionMethod method, CompressionLevel level, MemoryStream source);

    /// <summary>
    /// Decompresses data from the source stream and returns the decompressed data in an <see cref="IMemoryOwner{T}"/>.
    /// The returned memory owner contains the decompressed bytes. The source stream is read but not owned or disposed
    /// by this method.
    /// </summary>
    ValueTask<IMemoryOwner<byte>> Decompress(CompressionMethod method, Stream source, int destinationLength);
}

class DefaultCompressor : ICompressor {

    // "None" (no compression) as conversion helper

    private async ValueTask<IMemoryOwner<byte>> NoneCompress(MemoryStream source) {
        var r = MemoryOwner<byte>.Allocate((int)source.Length);
        source.Position = 0;
        await source.CopyToAsync(r.Memory);
        return r;
    }

    private async ValueTask<IMemoryOwner<byte>> NoneDecompress(Stream source, int destinationLength) {
        var r = MemoryOwner<byte>.Allocate(destinationLength);
        await source.CopyToAsync(r.Memory);
        return r;

    }

    // "Snappy" compression

    private async ValueTask<IMemoryOwner<byte>> SnappyCompress(MemoryStream source) {
        ReadOnlySpan<byte> src = source.GetBuffer().AsSpan(0, (int)source.Length);
        return Snappy.CompressToMemory(src);
    }

    private async ValueTask<IMemoryOwner<byte>> SnappyDecompress(Stream source) {
        byte[] compressed = source.ToByteArray()!;
        return Snappy.DecompressToMemory(compressed);
    }

    // "Gzip" compression

    private async ValueTask<IMemoryOwner<byte>> GzipCompress(MemoryStream source, CompressionLevel level) {
        // Compress into an in-memory stream and copy into MemoryOwner without extra temporary arrays
        using var ms = new MemoryStream();
        source.Position = 0;
        using(var gzip = new GZipStream(ms, level, leaveOpen: true)) {
            await source.CopyToAsync(gzip);
            await gzip.FlushAsync();
        }
        int len = (int)ms.Length;
        var owner = MemoryOwner<byte>.Allocate(len);
        // Use underlying buffer to copy bytes to owner
        byte[] buf = ms.GetBuffer();
        new ReadOnlySpan<byte>(buf, 0, len).CopyTo(owner.Span);
        return owner;
    }

    private async ValueTask<IMemoryOwner<byte>> GzipDecompress(Stream source, int destinationLength) {
        var owner = MemoryOwner<byte>.Allocate(destinationLength);
        int copied = 0;
        using var ms = new MemoryStream();
        await source.CopyToAsync(ms);
        ms.Position = 0;
        using(var gzip = new GZipStream(ms, CompressionMode.Decompress, leaveOpen: true)) {
            copied = await gzip.CopyToAsync(owner.Memory);
        }
        if(copied < destinationLength) {
            // IMPORTANT: .Slice() transfers ownership, so owner does not need to be disposed
            owner = owner.Slice(0, copied);
        }
        return owner;
    }

    // "LZO" compression

    private async ValueTask<IMemoryOwner<byte>> LzoCompress(MemoryStream source, CompressionLevel level) {
        throw new NotImplementedException();
    }

    private async ValueTask<IMemoryOwner<byte>> LzoDecompress(Stream source, int destinationLength) {
        throw new NotImplementedException();
    }

    // "Brotli" compression

    private async ValueTask<IMemoryOwner<byte>> BrotliCompress(MemoryStream source, CompressionLevel level) {
        using var ms = new MemoryStream();
        source.Position = 0;
        using(BrotliStream? brotli = new BrotliStream(ms, level, leaveOpen: true)) {
            await source.CopyToAsync(brotli);
        }
        int len = (int)ms.Length;
        var owner = MemoryOwner<byte>.Allocate(len);
        byte[] buf = ms.GetBuffer();
        new ReadOnlySpan<byte>(buf, 0, len).CopyTo(owner.Span);
        return owner;
    }

    private async ValueTask<IMemoryOwner<byte>> BrotliDecompress(Stream source, int destinationLength) {
        var owner = MemoryOwner<byte>.Allocate(destinationLength);
        using var brotli = new BrotliStream(source, CompressionMode.Decompress, leaveOpen: true);

        byte[] buffer = ArrayPool<byte>.Shared.Rent(81920);
        try {
            int totalRead = 0;
            while(totalRead < destinationLength) {
                int toRead = Math.Min(buffer.Length, destinationLength - totalRead);
                int read = await brotli.ReadAsync(buffer, 0, toRead);
                if(read == 0)
                    break;
                new ReadOnlySpan<byte>(buffer, 0, read).CopyTo(owner.Span.Slice(totalRead, read));
                totalRead += read;
            }
        } finally {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return owner;
    }

    // "Zstandard" compression (uses forward compatible "SystemIOCompression.Zstandard.Backporting", as Zstandard will be part of .NET 11)

    /*
    private async ValueTask<IMemoryOwner<byte>> ZstdCompress(MemoryStream source, CompressionLevel level) {
        int zLevel = level switch {
            CompressionLevel.Optimal => 3,
            CompressionLevel.Fastest => 1,
            CompressionLevel.NoCompression => 1,
            CompressionLevel.SmallestSize => 19,
            _ => 0
        };

        long maxDestLength = ZstandardEncoder.GetMaxCompressedLength(source.Length);
        MemoryOwner<byte> owner = MemoryOwner<byte>.Allocate((int)maxDestLength);
        bool ok = ZstandardEncoder.TryCompress(source.GetBuffer().AsSpan(0, (int)source.Length), owner.Memory.Span, out int compressedLength, zLevel, 0);
        if(!ok) {
            throw new InvalidOperationException("Compression failed");
        }
        return owner.Slice(0, compressedLength);
    }

    private async ValueTask<IMemoryOwner<byte>> ZstdDecompress(Stream source, int destinationLength) {

        long length = source.Length;
        using IMemoryOwner<byte> sourceMO = MemoryOwner<byte>.Allocate((int)length);
        await source.CopyToAsync(sourceMO.Memory);

        if(!ZstandardDecoder.TryGetMaxDecompressedLength(sourceMO.Memory.Span, out long destLength))
            throw new InvalidOperationException("Invalid compressed data (could not get length)");

        MemoryOwner<byte> destMO = MemoryOwner<byte>.Allocate((int)destLength);
        bool ok = ZstandardDecoder.TryDecompress(sourceMO.Memory.Span, destMO.Memory.Span, out int actualDestLength);
        if(!ok) {
            throw new InvalidOperationException("Decompression failed");
        }

        return destMO.Slice(0, actualDestLength);
    }
    */

    private async ValueTask<IMemoryOwner<byte>> ZstdCompress(MemoryStream source, CompressionLevel level) {
        int zLevel = level switch {
            CompressionLevel.Optimal => 3,
            CompressionLevel.Fastest => 1,
            CompressionLevel.NoCompression => 1,
            CompressionLevel.SmallestSize => 19,
            _ => 0
        };

        using var compressor = new ZstdSharp.Compressor(zLevel);
        ReadOnlySpan<byte> data = source.GetBuffer().AsSpan(0, (int)source.Length);
        Span<byte> compressed = compressor.Wrap(data);
        var owner = MemoryOwner<byte>.Allocate(compressed.Length);
        compressed.CopyTo(owner.Span);
        return owner;
    }

    private async ValueTask<IMemoryOwner<byte>> ZstdDecompress(Stream source, int destinationLength) {
        using var decompressor = new ZstdSharp.Decompressor();
        byte[] compressed = source.ToByteArray()!;
        // Do not pass destinationLength as maxDecompressedSize — some Parquet files report an incorrect
        // UncompressedPageSize in their metadata. Instead, let ZstdSharp read the actual content size
        // that is embedded in the Zstd frame header, which is always accurate.
        Span<byte> decompressed = decompressor.Unwrap(compressed);
        var owner = MemoryOwner<byte>.Allocate(decompressed.Length);
        decompressed.CopyTo(owner.Span);
        return owner;
    }

    // "LZ4" compression, requires K4os.Compression.LZ4 NuGet package

    private async ValueTask<IMemoryOwner<byte>> Lz4Compress(MemoryStream source, CompressionLevel level) {
        LZ4Level lz4Level = level switch {
            CompressionLevel.Optimal => LZ4Level.L11_OPT,
            CompressionLevel.Fastest => LZ4Level.L00_FAST,
            CompressionLevel.NoCompression => LZ4Level.L00_FAST,
            CompressionLevel.SmallestSize => LZ4Level.L12_MAX,
            _ => LZ4Level.L09_HC
        };

        ReadOnlySpan<byte> data = source.GetBuffer().AsSpan(0, (int)source.Length);
        int maxCompressedSize = LZ4Codec.MaximumOutputSize(data.Length);
        var owner = MemoryOwner<byte>.Allocate(maxCompressedSize);
        int compressedSize = LZ4Codec.Encode(
            data,
            owner.Span,
            level: lz4Level);
        return owner.Slice(0, compressedSize);
    }

    /// <summary>
    /// LZ4_RAW: a single raw LZ4 block.
    /// </summary>
    private async ValueTask<IMemoryOwner<byte>> Lz4RawDecompress(Stream source, int destinationLength) {
        byte[] compressed = source.ToByteArray()!;
        if(compressed.Length == 0)
            return MemoryOwner<byte>.Empty;   // e.g. the values section of an all-null data page V2
        var owner = MemoryOwner<byte>.Allocate(destinationLength);
        int decoded = LZ4Codec.Decode(compressed, owner.Span);
        if(decoded < 0) {
            owner.Dispose();
            throw new ParquetException($"{CompressionMethod.Lz4Raw} decompression failed, page data is not a valid LZ4 block");
        }

        // IMPORTANT: .Slice() transfers ownership, so owner does not need to be disposed
        return (decoded < destinationLength) ? owner.Slice(0, decoded) : owner;
    }

    /// <summary>
    /// Legacy LZ4 codec, which is ambiguous: parquet-mr (Hadoop Lz4Codec, used by Spark, Hive etc.) writes Hadoop
    /// frames, while parquet-cpp and this library write a single raw LZ4 block. Like Arrow, try Hadoop framing first
    /// and fall back to a raw block when the data does not parse as Hadoop frames.
    /// </summary>
    private async ValueTask<IMemoryOwner<byte>> Lz4Decompress(Stream source, int destinationLength) {
        byte[] compressed = source.ToByteArray()!;
        if(compressed.Length == 0)
            return MemoryOwner<byte>.Empty;   // e.g. the values section of an all-null data page V2
        var owner = MemoryOwner<byte>.Allocate(destinationLength);

        if(TryDecodeHadoopLz4(compressed, owner.Span, out int decoded)) {
            return (decoded < destinationLength) ? owner.Slice(0, decoded) : owner;
        }

        decoded = LZ4Codec.Decode(compressed, owner.Span);
        if(decoded < 0) {
            owner.Dispose();
            throw new ParquetException(
                $"LZ4 decompression failed, page data is neither Hadoop framed LZ4 nor a valid LZ4 block");
        }

        return (decoded < destinationLength) ? owner.Slice(0, decoded) : owner;
    }

    /// <summary>
    /// Decodes Hadoop Lz4Codec framing: one or more frames, each being a 4 byte big endian decompressed size, a 4 byte
    /// big endian compressed size and that many bytes of raw LZ4 block. Returns false, without throwing, when the input
    /// is not consistent with this framing, so that the caller can fall back to a raw block.
    /// </summary>
    internal static bool TryDecodeHadoopLz4(ReadOnlySpan<byte> input, Span<byte> output, out int decoded) {
        const int prefixLength = sizeof(uint) * 2;
        decoded = 0;
        int inputOffset = 0;

        while((input.Length - inputOffset) >= prefixLength) {
            uint frameDecompressed = BinaryPrimitives.ReadUInt32BigEndian(input.Slice(inputOffset));
            uint frameCompressed = BinaryPrimitives.ReadUInt32BigEndian(input.Slice(inputOffset + sizeof(uint)));
            inputOffset += prefixLength;

            if((frameCompressed > (uint)(input.Length - inputOffset)) ||
               (frameDecompressed > (uint)(output.Length - decoded))) {
                return false;
            }

            int frameDecoded = LZ4Codec.Decode(
                input.Slice(inputOffset, (int)frameCompressed),
                output.Slice(decoded, (int)frameDecompressed));
            if(frameDecoded != (int)frameDecompressed) {
                return false;
            }

            inputOffset += (int)frameCompressed;
            decoded += frameDecoded;
        }

        // every byte must belong to a frame, otherwise this was not Hadoop framing after all
        return (inputOffset == input.Length) && (decoded > 0);
    }

    public async ValueTask<IMemoryOwner<byte>> CompressAsync(
        CompressionMethod method, CompressionLevel level, MemoryStream source) {
        switch(method) {
            case CompressionMethod.None:
                return await NoneCompress(source);
            case CompressionMethod.Snappy:
                return await SnappyCompress(source);
            case CompressionMethod.Gzip:
                return await GzipCompress(source, level);
            case CompressionMethod.Lzo:
                return await LzoCompress(source, level);
            case CompressionMethod.Brotli:
                return await BrotliCompress(source, level);
#pragma warning disable CS0618 // obsolete for users, still written when asked for
            case CompressionMethod.LZ4:
#pragma warning restore CS0618
                return await Lz4Compress(source, level);
            case CompressionMethod.Zstd:
                return await ZstdCompress(source, level);
            case CompressionMethod.Lz4Raw:
                return await Lz4Compress(source, level);
            default:
                throw new NotSupportedException($"Compression method {method} is not supported.");
        }
    }

    public async ValueTask<IMemoryOwner<byte>> Decompress(
        CompressionMethod method, Stream source, int destinationLength) {
        switch(method) {
            case CompressionMethod.None:
                return await NoneDecompress(source, destinationLength);
            case CompressionMethod.Snappy:
                return await SnappyDecompress(source);
            case CompressionMethod.Gzip:
                return await GzipDecompress(source, destinationLength);
            case CompressionMethod.Lzo:
                return await LzoDecompress(source, destinationLength);
            case CompressionMethod.Brotli:
                return await BrotliDecompress(source, destinationLength);
#pragma warning disable CS0618 // obsolete for writing only, files using it are still read
            case CompressionMethod.LZ4:
#pragma warning restore CS0618
                return await Lz4Decompress(source, destinationLength);
            case CompressionMethod.Zstd:
                return await ZstdDecompress(source, destinationLength);
            case CompressionMethod.Lz4Raw:
                return await Lz4RawDecompress(source, destinationLength);
            default:
                throw new NotSupportedException($"Compression method {method} is not supported.");
        }

    }
}

/// <summary>
/// Compressor entry point.
/// </summary>
static class Compressor {
    /// <summary>
    /// Gets the default instance of the compressor to use for compression and decompression operations.
    /// </summary>
    /// <remarks>
    /// This property provides a shared, thread-safe compressor instance that can be used throughout the application.
    /// Use this instance when a custom compressor is not required.
    /// </remarks>
    public static ICompressor Instance { get; } = new DefaultCompressor();
}
