using System;

namespace Terminal.Core.ExtensionSpace
{
  public static class DateTimeExtensions
  {
    public static DateTime? Round(this DateTime? input, TimeSpan? span)
    {
      if (input == null || span == null)
      {
        return null;
      }

      return new DateTime(input.Value.Ticks - (input.Value.Ticks % span.Value.Ticks), input.Value.Kind);
    }
  }
}