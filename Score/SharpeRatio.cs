using MathNet.Numerics.Statistics;
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
  /// Dev = Series deviation
  /// AnnDev = Dev * Sqrt(Days)
  /// Sharpe = Mean([Ra - Rb]) / Dev([Ra - Rb])
  /// Using CAGR
  /// Sharpe = (CAGR - IR) / AnnDev
  /// Using AHPR
  /// Sharpe = (AHPR - (1 + IR)) / Dev
  /// </summary>
  public class SharpeRatio
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
    /// <param name="option"></param>
    /// <returns></returns>
    public virtual double Calculate(int? option = 0)
    {
      if (Values.Any() == false)
      {
        return 0.0;
      }

      switch (option)
      {
        case 0: return CalculateDealsRatio();
        case 1: return CalculateAverageRatio();
        case 2: return CalculateCompoundRatio();
      }

      return 0.0;
    }

    /// <summary>
    /// Calculate SR based on mean
    /// </summary>
    /// <param name="option"></param>
    /// <returns></returns>
    public virtual double CalculateDealsRatio()
    {
      var input = Values.FirstOrDefault();
      var output = Values.LastOrDefault();

      if (input == null || output == null)
      {
        return 0.0;
      }

      var excessGain = Values.Select((o, i) => o.Value - Values.ElementAtOrDefault(i - 1)?.Value ?? 0.0).Mean();
      var deviation = Values.Select(o => o.Value).StandardDeviation();

      if (deviation == 0)
      {
        return 0.0;
      }

      return excessGain / deviation;
    }

    /// <summary>
    /// Calculate SR based on AHPR
    /// </summary>
    /// <param name="option"></param>
    /// <returns></returns>
    public virtual double CalculateAverageRatio()
    {
      var input = Values.FirstOrDefault();
      var output = Values.LastOrDefault();

      if (input == null || output == null)
      {
        return 0.0;
      }

      var score = new AHPR
      {
        Values = Values
      };

      var excessGain = score.Calculate() - InterestRate;
      var deviation = Values.Select(o => o.Value).StandardDeviation();

      if (deviation == 0)
      {
        return 0.0;
      }

      return excessGain / deviation;
    }

    /// <summary>
    /// Calculate
    /// </summary>
    /// <param name="option"></param>
    /// <returns></returns>
    public virtual double CalculateCompoundRatio(int? option = 0)
    {
      var input = Values.FirstOrDefault();
      var output = Values.LastOrDefault();

      if (input == null || output == null)
      {
        return 0.0;
      }

      var score = new CAGR
      {
        Values = Values
      };

      var excessGain = score.Calculate() - InterestRate;
      var days = output.Time.Subtract(input.Time).Duration().Days + 1;
      var deviation = Values.Select(o => o.Value).StandardDeviation() * Math.Sqrt(days);

      if (deviation == 0)
      {
        return 0.0;
      }

      return excessGain / deviation;
    }
  }
}
