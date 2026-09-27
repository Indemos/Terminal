using System;
using System.Runtime.CompilerServices;

namespace Core.Extensions
{
  public static class DateTimeExtensions
  {
    private const long UnixEpochTicks = 621355968000000000L; // 1970-01-01 UTC
    private const long TicksPerMicrosecond = 10L; // 1 tick = 100ns

    /// <summary>
    /// Round by interval
    /// </summary>
    /// <param name="input"></param>
    /// <param name="span"></param>
    public static long? Round(this long? input, TimeSpan? span)
    {
      if (input is null)
      {
        return null;
      }

      var date = input.Value;
      var excess = Math.Max(span?.Ticks ?? 1, 1);

      return date - (date % excess);
    }

    /// <summary>
    /// Round by interval
    /// </summary>
    /// <param name="input"></param>
    /// <param name="span"></param>
    public static DateTime? Round(this DateTime? input, TimeSpan? span)
    {
      if (input is null)
      {
        return null;
      }

      var date = input.Value.Ticks;
      var excess = Math.Max(span?.Ticks ?? 1, 1);

      return new DateTime(date - (date % excess), input.Value.Kind);
    }

    /// <summary>
    /// Date without time
    /// </summary>
    /// <param name="input"></param>
    public static DateTime ToDateTime(this long input)
    {
      if (input is 0) return DateTime.MinValue;

      var minTicks = 0L;
      var minRange = long.MaxValue;
      var nowTicks = DateTime.UtcNow.Ticks;
      var spread = DateTime.MaxValue.Ticks - DateTime.MinValue.Ticks;

      void Consider(long version)
      {
        // Fast range check without branch for overflow
        if (version < DateTime.MinValue.Ticks || version > DateTime.MaxValue.Ticks)
        {
          return;
        }

        var range = version - nowTicks;

        if (range < 0) range = -range;
        if (range < minRange) { minRange = range; minTicks = version; }
      }

      // 1. Input is already ticks
      Consider(input);

      // 2. seconds: ticks = epoch + input * TicksPerSecond bounds computed from Min/Max, no hardcoded numbers
      var minSec = (DateTime.MinValue.Ticks - UnixEpochTicks) / TimeSpan.TicksPerSecond;
      var maxSec = (DateTime.MaxValue.Ticks - UnixEpochTicks) / TimeSpan.TicksPerSecond;

      if (input >= minSec && input <= maxSec)
      {
        Consider(UnixEpochTicks + input * TimeSpan.TicksPerSecond);
      }

      // 3. milliseconds
      var minMs = (DateTime.MinValue.Ticks - UnixEpochTicks) / TimeSpan.TicksPerMillisecond;
      var maxMs = (DateTime.MaxValue.Ticks - UnixEpochTicks) / TimeSpan.TicksPerMillisecond;

      if (input >= minMs && input <= maxMs)
      {
        Consider(UnixEpochTicks + input * TimeSpan.TicksPerMillisecond);
      }

      // 4. microseconds: 10 ticks = 1us
      var minUs = (DateTime.MinValue.Ticks - UnixEpochTicks) / TicksPerMicrosecond;
      var maxUs = (DateTime.MaxValue.Ticks - UnixEpochTicks) / TicksPerMicrosecond;

      if (input >= minUs && input <= maxUs)
      {
        Consider(UnixEpochTicks + input * TicksPerMicrosecond);
      }

      // 5. nanoseconds: 1 tick = 100ns
      Consider(UnixEpochTicks + input / 100L);

      return minRange == long.MaxValue ? DateTime.MinValue : new DateTime(minTicks, DateTimeKind.Utc);
    }
  }
}
