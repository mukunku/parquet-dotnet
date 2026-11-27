using System;

namespace Parquet;

/// <summary>
/// How to handle dates and timestamps (DATE, TIMESTAMP and INT96) that cannot be represented as <see cref="DateTime"/>
/// or <see cref="DateOnly"/>, i.e. that fall outside years 1 to 9999. See <see cref="ParquetOptions.DateTimeOutOfRangeHandling"/>.
/// </summary>
public enum DateTimeOutOfRangeHandling {
    /// <summary>
    /// Throw <see cref="ParquetException"/> naming the column and the raw value.
    /// </summary>
    Throw = 0,

    /// <summary>
    /// Replace values before year 1 with the type's MinValue and values after year 9999 with its MaxValue
    /// (<see cref="DateTime.MinValue"/> / <see cref="DateTime.MaxValue"/>, or <see cref="DateOnly.MinValue"/> /
    /// <see cref="DateOnly.MaxValue"/>). Note that this changes data, so the original value is lost. Some writers use
    /// such values on purpose, e.g. DuckDB stores <c>infinity</c> and <c>-infinity</c> as the largest and smallest
    /// possible values.
    /// </summary>
    Clamp = 1
}
