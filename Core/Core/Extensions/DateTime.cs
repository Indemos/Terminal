using System;

namespace Core.Extensions
{
  public static class DateTimeExtensions
  {
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

      return new DateTime(date - (date % excess)).Ticks;
    }

    /// <summary>
    /// Date without time
    /// </summary>
    /// <param name="input"></param>
    public static DateOnly? AsDate(this DateTime? input)
    {
      if (input is null)
      {
        return null;
      }

      return DateOnly.FromDateTime(input.Value);
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

      return input.Value.Round(span);
    }

    /// <summary>
    /// Round by interval
    /// </summary>
    /// <param name="input"></param>
    /// <param name="span"></param>
    public static DateTime Round(this DateTime input, TimeSpan? span)
    {
      var date = input.Ticks;
      var excess = Math.Max(span?.Ticks ?? 1, 1);

      return new DateTime(date - (date % excess), input.Kind);
    }
  }
}
