using System;

namespace Core.Common.Extensions
{
  public static class DoubleExtensions
  {
    /// <summary>
    /// Equality with precision
    /// </summary>
    /// <param name="input"></param>
    /// <param name="num"></param>
    /// <param name="precision"></param>
    public static bool Is(this double? input, double? num, double precision = double.Epsilon)
    {
      return Math.Abs((input - num).Value) < precision;
    }

    /// <summary>
    /// Equality with precision
    /// </summary>
    /// <param name="input"></param>
    /// <param name="num"></param>
    /// <param name="precision"></param>
    public static bool IsNot(this double? input, double? num, double precision = double.Epsilon)
    {
      return input.Is(num, precision) is false;
    }

    /// <summary>
    /// Equality with precision
    /// </summary>
    /// <param name="input"></param>
    /// <param name="num"></param>
    /// <param name="precision"></param>
    public static bool IsGt(this double? input, double? num, double precision = double.Epsilon)
    {
      return input > num - precision;
    }

    /// <summary>
    /// Equality with precision
    /// </summary>
    /// <param name="input"></param>
    /// <param name="num"></param>
    /// <param name="precision"></param>
    public static bool IsLt(this double? input, double? num, double precision = double.Epsilon)
    {
      return input < num + precision;
    }

    /// <summary>
    /// Equality with precision
    /// </summary>
    /// <param name="input"></param>
    /// <param name="num"></param>
    /// <param name="precision"></param>
    public static bool IsGte(this double? input, double? num, double precision = double.Epsilon)
    {
      return input >= num - precision;
    }

    /// <summary>
    /// Equality with precision
    /// </summary>
    /// <param name="input"></param>
    /// <param name="num"></param>
    /// <param name="precision"></param>
    public static bool IsLte(this double? input, double? num, double precision = double.Epsilon)
    {
      return input <= num + precision;
    }
  }
}
