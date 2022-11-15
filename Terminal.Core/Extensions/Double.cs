using System;

namespace Terminal.Core.ExtensionSpace
{
  public static class DoubleExtensions
  {
    public static double ToDouble(this double? input, double value = 0)
    {
      if (input is null)
      {
        return value;
      }

      return input.Value;
    }

    public static decimal ToDecimal(this double input)
    {
      return (decimal) input;
    }

    public static bool IsEqual(this double input, double num, double epsilon = double.Epsilon)
    {
      return Math.Abs(input - num) < epsilon;
    }
  }
}
