using System;

namespace Core.Common.Extensions
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
    public static DateOnly? AsDate(this DateTime? input)
    {
      if (input is null)
      {
        return null;
      }

      return DateOnly.FromDateTime(input.Value);
    }
  }
}
