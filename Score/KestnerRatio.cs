using MathNet.Numerics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ScoreSpace
{
  /// <summary>
  /// Kestner ratio or K-Ratio
  /// Deviation from the expected returns curve
  /// KR = Kestner ratio
  /// N = Number of observations
  /// Slope = Coefficient that defines how steep the optimal regression line should be
  /// Error = Standard error explaining deviation from regression
  /// KR = Slope / Error / N
  /// </summary>
  public class KestnerRatio
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
      var count = Values.Count();

      if (count == 0)
      {
        return 0.0;
      }

      var deviation = 0.0;
      var regression = new double[count];
      var slope = Math.Log(Values.Last().Value) / count;

      for (var i = 0; i < count; i++)
      {
        var v = Values.ElementAtOrDefault(i)?.Value ?? 0.0;
        var log = Math.Log(v);

        if (double.IsNaN(log) == false)
        {
          regression[i] = regression.ElementAtOrDefault(i - 1) + slope;
          deviation += Math.Pow(regression[i] - log, 2);
        }
      }

      var error = Math.Sqrt(deviation / count) / Math.Sqrt(count);

      return slope / (error * count);
    }
  }
}
