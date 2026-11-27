namespace Parquet;

/// <summary>
/// Parquet compression method. See https://github.com/apache/parquet-format/blob/master/Compression.md
/// </summary>
public enum CompressionMethod {
    /// <summary>
    /// No compression
    /// </summary>
    None = 0,

    /// <summary>
    /// Snappy compression 
    /// </summary>
    Snappy = 1,

    /// <summary>
    /// Gzip compression
    /// </summary>
    Gzip = 2,

    /// <summary>
    /// LZO
    /// </summary>
    Lzo = 3,

    /// <summary>
    /// Brotli
    /// </summary>
    Brotli = 4,

    /// <summary>
    /// Legacy LZ4 codec, deprecated in the Parquet format in favour of <see cref="Lz4Raw"/>. 
    /// Files using it are still read, whichever writer made them.
    /// </summary>
    [System.Obsolete(
        "The LZ4 codec is deprecated in the Parquet format because writers never agreed on it. " +
        "Use Lz4Raw instead, which current readers support. LZ4 still works for reading and writing as before.")]
    LZ4 = 5,

    /// <summary>
    /// ZSTD
    /// </summary>
    Zstd = 6,

    /// <summary>
    /// LZ4 raw
    /// </summary>
    Lz4Raw = 7,
}
