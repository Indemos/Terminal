using System;
using System.Collections.Generic;
using System.Linq;

namespace ScoreSpace
{
  /// <summary>
  /// Ulcer index ratio
  /// Measures downside volatility of the deviation
  /// IR = Interest Rate
  /// CAGR = Compound annual growth rate
  /// DD = Drawdown when the previous return is greater then the next
  /// RMSDD = Root mean square of all drawdowns in a series = (Sum(DD) / Count(DD)) ^ (1 / 2)
  /// MAR = (CAGR - IR) / RMSDD
  /// </summary>
  public class MartinRatio
  {
    /// <summary>
    /// Input values
    /// </summary>
    public virtual IEnumerable<InputData> Values { get; set; } = new List<InputData>();

    /// <summary>
    /// Interest rate
    /// </summary>
    public virtual double? InterestRate { get; set; } = 0.0;

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

      if (count == 0)
      {
        return 0.0;
      }

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

      return (cagr.Calculate() - InterestRate.Value) / Math.Sqrt(averageLoss);
    }
  }
}
