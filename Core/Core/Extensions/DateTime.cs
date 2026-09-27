using System;

namespace Core.Extensions
{
  public static class DateTimeExtensions
  {
    private static readonly DateTimeOffset MinUnix = new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

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
      if (input is 0) return DateTime.MinValue; // 0000-00-00

      var now = DateTimeOffset.UtcNow;
      var minRange = long.MaxValue;

      DateTimeOffset response = default;

      void Compare(Func<DateTimeOffset> version)
      {
        try
        {
          var date = version();
          var range = Math.Abs((date - now).Ticks); // closest to now wins

          if (range < minRange)
          {
            minRange = range;
            response = date;
          }
        }
        catch { }
      }

      if (input >= DateTime.MinValue.Ticks && input <= DateTime.MaxValue.Ticks)
      {
        Compare(() => new DateTimeOffset(new DateTime(input, DateTimeKind.Utc)));
      }

      Compare(() => DateTimeOffset.FromUnixTimeSeconds(input));
      Compare(() => DateTimeOffset.FromUnixTimeMilliseconds(input));
      Compare(() => MinUnix.AddTicks(input * 10));  // us
      Compare(() => MinUnix.AddTicks(input / 100)); // ns

      return minRange == long.MaxValue ? DateTime.MinValue : response.UtcDateTime;
    }
  }
}
