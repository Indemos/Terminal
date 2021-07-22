using System;
using System.Collections.Generic;
using System.Linq;

namespace ScoreSpace
{
  /// <summary>
  /// Sterling ratio
  /// A ratio between returns and average loss
  /// IR = Interest Rate
  /// CAGR = Compound annual growth rate
  /// DD = Drawdown when the previous return is greater then the next
  /// AVGDD = Root mean square of all drawdowns in a series = Sum(DD) / Count(DD)
  /// MAR = (CAGR - IR) / AVGDD
  /// </summary>
  public class SterlingRatio
  {
    /// <summary>
    /// Input values
    /// </summary>
    public virtual IEnumerable<InputData> Values { get; set; } = new List<InputData>();

    /// <summary>
    /// Interest rate
    /// </summary>
    public virtual double InterestRate { get; set; } = 0.0;

    /// <summary>
    /// Calculate
    /// </summary>
    /// <returns></returns>
    public virtual double Calculate()
    {
      var cagr = new CAGR
      {
        Values = Values
      };

      var count = Values.Count();
      var losses = new List<double>();

      for (var i = 1; i < count; i++)
      {
        var currentValue = Values.ElementAtOrDefault(i)?.Value ?? 0;
        var previousValue = Values.ElementAtOrDefault(i - 1)?.Value ?? 0;

        if (previousValue > currentValue)
        {
          losses.Add(Math.Pow(previousValue - currentValue, 2));
        }
      }

      var averageLoss = losses.Any() ? losses.Average() : 1.0;

      return (cagr.Calculate() - InterestRate) / averageLoss;
    }
  }
}
