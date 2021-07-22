using System;
using System.Collections.Generic;
using System.Linq;

namespace ScoreSpace
{
  /// <summary>
  /// MAR ratio
  /// Minimum acceptance return or a ratio between returns and max loss
  /// CAGR = Compound annual growth rate
  /// DD = Maximum drawdown in a series
  /// MAR = CAGR / DD
  /// </summary>
  public class MAR
  {
    /// <summary>
    /// Input values
    /// </summary>
    public virtual IEnumerable<InputData> Values { get; set; } = new List<InputData>();

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

      var maxLoss = 0.0;
      var count = Values.Count();

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
          maxLoss = Math.Max(maxLoss, previousValue - currentValue);
        }
      }

      if (maxLoss == 0)
      {
        return 0.0;
      }

      return cagr.Calculate() / maxLoss;
    }
  }
}
