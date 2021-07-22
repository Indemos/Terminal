using MathNet.Numerics.Financial;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ScoreSpace
{
  /// <summary>
  /// Reward to risk ratio for a selected period
  /// Ra = Asset returns
  /// Rb = Risk-free returns
  /// IR = Interest rate
  /// DownDev = Series deviation below 0 level
  /// AnnDev = DownDev * (Days ^ (1 / 2))
  /// Sortino = (CAGR - IR) / AnnDev
  /// </summary>
  public class SortinoRatio
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
      var input = Values.FirstOrDefault();
      var output = Values.LastOrDefault();

      if (input == null || output == null)
      {
        return 0.0;
      }

      var cagr = new CAGR
      {
        Values = Values
      };

      var values = Values.Select((o, i) => o.Value - Values.ElementAtOrDefault(i - 1)?.Value ?? 0.0);
      var excessGain = cagr.Calculate() - InterestRate;
      var days = output.Time.Subtract(input.Time).Duration().Days + 1.0;
      var downsideDeviation = values.DownsideDeviation(0);
      var annualDeviation = (double.IsNaN(downsideDeviation) ? 0.0 : downsideDeviation) * Math.Sqrt(days);

      if (annualDeviation == 0)
      {
        return 0.0;
      }

      return excessGain / annualDeviation;
    }
  }
}
