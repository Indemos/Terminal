using System;

namespace Terminal.Core.ExtensionSpace
{
  public static class DateTimeExtensions
  {
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
  }
}
