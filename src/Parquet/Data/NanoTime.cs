using System;
using System.Buffers.Binary;
using System.IO;

namespace Parquet.Data;

readonly struct NanoTime {
    private readonly int _julianDay;
    private readonly long _timeOfDayNanos;
    public const int BinarySize = 12;

    public NanoTime(Span<byte> span) {
        if(span.Length != BinarySize)
            throw new ArgumentException($"NanoTime bytes must be {BinarySize} bytes long");

        _timeOfDayNanos = BitConverter.ToInt64(span.Slice(0, sizeof(long)));
        _julianDay = BitConverter.ToInt32(span.Slice(sizeof(long)));
    }

    public NanoTime(DateTime dt) {
        dt = dt.ToUniversalTime();
        int m = dt.Month;
        int d = dt.Day;
        int y = dt.Year;

        if(m < 3) {
            m += 12;
            y -= 1;
        }

        _julianDay = d + (((153 * m) - 457) / 5) + (365 * y) + (y / 4) - (y / 100) + (y / 400) + 1721119;
        _timeOfDayNanos = dt.TimeOfDay.Ticks * 100;
    }

    public void Write(Stream s) {
        Span<byte> buffer = stackalloc byte[sizeof(long) + sizeof(int)];
        BinaryPrimitives.WriteInt64LittleEndian(buffer, _timeOfDayNanos);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[sizeof(long)..], _julianDay);
        s.Write(buffer);
    }

    public byte[] GetBytes() {
        byte[] r = new byte[sizeof(long) + sizeof(int)];
        BinaryPrimitives.WriteInt64LittleEndian(r, _timeOfDayNanos);
        BinaryPrimitives.WriteInt32LittleEndian(r.AsSpan(sizeof(long)), _julianDay);
        return r;
    }
    
    /// <summary>
    /// Julian day number of 0001-01-01 (<see cref="DateTime.MinValue"/>), both being proleptic Gregorian.
    /// </summary>
    private const long JulianDayOfDateTimeMinValue = 1721426;

    public DateTime ToDateTime(DateTimeOutOfRangeHandling outOfRange = DateTimeOutOfRangeHandling.Throw, string? columnName = null) {
        // Int128 so that any julian day / nanos combination, including garbage, cannot overflow
        Int128 ticks = ((Int128)_julianDay - JulianDayOfDateTimeMinValue) * TimeSpan.TicksPerDay + (_timeOfDayNanos / 100);

        if(!OtherExtensions.TryTicksToDateTime(ticks, DateTimeKind.Unspecified, outOfRange, out DateTime result))
            throw OtherExtensions.OutOfRangeException(
                $"INT96 timestamp (julian day {_julianDay}, {_timeOfDayNanos} nanoseconds of day)", columnName, typeof(DateTime));

        return result;
    }
}