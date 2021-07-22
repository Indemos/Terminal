using System;
using System.Collections.Generic;
using System.Linq;

namespace ScoreSpace
{
  /// <summary>
  /// LR Correlation
  /// A correlation between a series and its linear regression
  /// X = Observations
  /// Y = Regression line
  /// N = Number of observations
  /// Mean(X) = Arithmetic average = Sum(X) / N
  /// Var(X) = Variance = Sum(((X - Mean(X)) ^ 2) / N
  /// Dev(X) = Var(X) ^ (1 / 2)
  /// Cov(X, Y) = Covariance between X and Y series = Sum((X - Mean(X)) * (Y - Mean(Y))) / N
  /// Cor(X, Y) = Correlation between X and Y series = Cov(X, Y) / (Dev(X) * Dev(Y))
  /// </summary>
  public class RegressionCorrelation
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
      var seriesX = Values
        .Select(o => o.Value)
        .Where(o => o != 0)
        .ToList();

      if (seriesX.Count == 0)
      {
        return 0.0;
      }

      var original = seriesX.First();
      var slope = seriesX.Last() / seriesX.Count;
      var seriesY = seriesX.Select((o, i) => seriesX.ElementAtOrDefault(i - 1) + slope).ToList();
      var averageX = seriesX.Average();
      var averageY = seriesY.Average();
      var covariance = 0.0;
      var varianceX = 0.0;
      var varianceY = 0.0;

      for (var i = 0; i < seriesX.Count; i++)
      {
        var x = seriesX[i] - averageX;
        var y = seriesY[i] - averageY;

        varianceX += Math.Pow(x, 2);
        varianceY += Math.Pow(y, 2);
        covariance += x * y;
      }

      varianceX /= seriesX.Count;
      varianceY /= seriesY.Count;
      covariance /= seriesX.Count;

      var deviation = Math.Sqrt(varianceX) * Math.Sqrt(varianceY);

      if (deviation == 0)
      {
        return 0.0;
      }

      return covariance / deviation;
    }
  }
}
