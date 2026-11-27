using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Parquet.Schema;

namespace Parquet;

static class OtherExtensions {
    private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private const long UnixEpochMilliseconds = 62_135_596_800_000L;
    private const long UnixEpochMicroseconds = 62_135_596_800_000_000L;

#if NET7_0_OR_GREATER
    private static readonly long UnixEpochNanoseconds = UnixEpoch.Ticks * TimeSpan.NanosecondsPerTick;
#endif

    public static DateTimeOffset FromUnixMilliseconds(this long unixMilliseconds) {
        return UnixEpoch.AddMilliseconds(unixMilliseconds);
    }

    public static long ToUnixMilliseconds(this DateTime dto) {
        long milliseconds = dto.Ticks / TimeSpan.TicksPerMillisecond;
        return milliseconds - UnixEpochMilliseconds;
    }

#if NET7_0_OR_GREATER
    public static long ToUnixMicroseconds(this DateTime dto) {
        long microseconds = dto.Ticks / TimeSpan.TicksPerMicrosecond;
        return microseconds - UnixEpochMicroseconds;
    }

    public static long ToUnixNanoseconds(this DateTime dto) {
        long nanoseconds = dto.Ticks * TimeSpan.NanosecondsPerTick;
        return nanoseconds - UnixEpochNanoseconds;
    }
#endif

    /// <summary>Day number (as in <see cref="DateOnly.DayNumber"/>) of 1970-01-01.</summary>
    private const int UnixEpochDayNumber = 719_162;

    /// <summary>
    /// Converts ticks since 0001-01-01 into <see cref="DateTime"/>. Ticks are <see cref="Int128"/> so that callers can
    /// compute them from any raw file value without overflowing. Returns false only when the value is out of range and
    /// <paramref name="outOfRange"/> is <see cref="DateTimeOutOfRangeHandling.Throw"/>; the caller then throws
    /// <see cref="OutOfRangeException"/>, so that the error message is only built when needed.
    /// </summary>
    public static bool TryTicksToDateTime(Int128 ticks, DateTimeKind kind, DateTimeOutOfRangeHandling outOfRange,
        out DateTime result) {
        if(ticks >= 0 && ticks <= DateTime.MaxValue.Ticks) {
            result = new DateTime((long)ticks, kind);
            return true;
        }

        if(outOfRange == DateTimeOutOfRangeHandling.Clamp) {
            result = DateTime.SpecifyKind(ticks < 0 ? DateTime.MinValue : DateTime.MaxValue, kind);
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Converts a unix time value (in units of <paramref name="ticksPerUnit"/>, e.g. 10 for microseconds) into
    /// <see cref="DateTime"/>, see <see cref="TryTicksToDateTime"/>.
    /// </summary>
    public static bool TryUnixToDateTime(long value, long ticksPerUnit, DateTimeKind kind,
        DateTimeOutOfRangeHandling outOfRange, out DateTime result) =>
        TryTicksToDateTime((Int128)value * ticksPerUnit + UnixEpoch.Ticks, kind, outOfRange, out result);

    /// <summary>
    /// Converts a unix time value expressed in nanoseconds into <see cref="DateTime"/>, truncating to ticks.
    /// </summary>
    public static bool TryUnixNanosToDateTime(long nanos, DateTimeKind kind,
        DateTimeOutOfRangeHandling outOfRange, out DateTime result) =>
        TryTicksToDateTime((Int128)(nanos / 100) + UnixEpoch.Ticks, kind, outOfRange, out result);

    /// <summary>
    /// Converts days since 1970-01-01 (parquet DATE) into <see cref="DateOnly"/>, see <see cref="TryTicksToDateTime"/>.
    /// </summary>
    public static bool TryUnixDaysToDateOnly(int unixDays, DateTimeOutOfRangeHandling outOfRange, out DateOnly result) {
        long dayNumber = (long)unixDays + UnixEpochDayNumber;
        if(dayNumber >= DateOnly.MinValue.DayNumber && dayNumber <= DateOnly.MaxValue.DayNumber) {
            result = DateOnly.FromDayNumber((int)dayNumber);
            return true;
        }

        if(outOfRange == DateTimeOutOfRangeHandling.Clamp) {
            result = dayNumber < 0 ? DateOnly.MinValue : DateOnly.MaxValue;
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Exception for a value that <see cref="DateTimeOutOfRangeHandling.Throw"/> rejected.
    /// </summary>
    /// <param name="rawValue">Description of the raw file value, e.g. "DATE value 2147483647 (days since 1970-01-01)"</param>
    /// <param name="columnName">Column the value belongs to</param>
    /// <param name="targetType">Type the value was converted to</param>
    public static ParquetException OutOfRangeException(string rawValue, string? columnName, Type targetType) =>
        new ParquetException(
            $"{rawValue} in column '{columnName}' is outside the range {targetType.Name} can represent (years 1 to 9999). " +
            $"Set {nameof(ParquetOptions)}.{nameof(ParquetOptions.DateTimeOutOfRangeHandling)} to " +
            $"{nameof(DateTimeOutOfRangeHandling.Clamp)} to clamp such values.");

    public static int ToUnixDays(this DateTime dto) {
        TimeSpan diff = dto - UnixEpoch;
        return (int)diff.TotalDays;
    }

#if NET6_0_OR_GREATER
    public static int ToUnixDays(this DateOnly dto) {
        TimeSpan diff = new DateTime(dto.Year, dto.Month, dto.Day) - UnixEpoch;
        return (int)diff.TotalDays;
    }
#endif

    public static DateTime ToUtc(this DateTime dto) =>
        dto.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(dto, DateTimeKind.Utc)
            : dto.ToUniversalTime();

    public static string AddPath(this string s, params string[] parts) {
        var path = new List<string>(parts.Length + 1);

        if(s != null)
            path.Add(s);
        if(parts != null)
            path.AddRange(parts.Where(p => p != null));

        return string.Join(ParquetSchema.PathSeparator, path);
    }

    public static bool EqualTo(this Array left, Array right) {
        if(left.Length != right.Length)
            return false;

        for(int i = 0; i < left.Length; i++) {
            object? il = left.GetValue(i);
            object? ir = right.GetValue(i);

            if(il == null || ir == null) {
                return il == null && ir == null;
            }

            if(!il.Equals(ir))
                return false;
        }

        return true;
    }

    public static Exception NotImplemented(string reason) {
        return new NotImplementedException($"{reason} is not yet implemented, and we are fully aware of it. From here you can either raise an issue on GitHub, or implemented it and raise a PR.");
    }

    #region [ byte[] ]

    public static string ToHexString(this byte[] bytes) {
#if NET5_0_OR_GREATER
        return Convert.ToHexString(bytes);
#else
        return BitConverter.ToString(bytes).Replace("-", "");
#endif
    }

    #endregion

    #region [ Stream ]

    /// <summary>
    /// Reads all stream in memory and returns as byte array
    /// </summary>
    public static byte[]? ToByteArray(this Stream? stream) {
        if(stream == null)
            return null;

        if(stream is MemoryStream ms1)
            return ms1.ToArray();

        using(var ms = new MemoryStream()) {
            stream.CopyTo(ms);
            return ms.ToArray();
        }
    }

    #endregion
}