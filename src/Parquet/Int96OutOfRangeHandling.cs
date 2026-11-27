using System;

namespace Parquet;

/// <summary>
/// How to handle INT96 timestamps that cannot be represented as <see cref="DateTime"/>.
/// See <see cref="ParquetOptions.Int96OutOfRangeHandling"/>.
/// </summary>
public enum Int96OutOfRangeHandling {
    /// <summary>
    /// Throw <see cref="ParquetException"/> naming the column and the raw value.
    /// </summary>
    Throw = 0,

    /// <summary>
    /// Replace values before year 1 with <see cref="DateTime.MinValue"/> and values after year 9999 with
    /// <see cref="DateTime.MaxValue"/>. Note that this changes data, so the original value is lost.
    /// </summary>
    Clamp = 1
}
