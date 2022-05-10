using System;

namespace Terminal.Core.ExtensionSpace
{
  public static class DateTimeExtensions
  {
    public static DateTime? Round(this DateTime? input, TimeSpan? span)
    {
      if (input is null || span is null)
      {
        return null;
      }

      return new DateTime(input.Value.Ticks - (input.Value.Ticks % span.Value.Ticks), input.Value.Kind);
    }
  }
}
