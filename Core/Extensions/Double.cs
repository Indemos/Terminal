using System;

namespace Terminal.Core.Extensions
{
  public static class DoubleExtensions
  {
    public static bool Is(this double input, double num, double precision = double.Epsilon)
    {
      return Math.Abs(input - num) < precision;
    }
  }
}
