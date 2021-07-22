using Core.ManagerSpace;
using ScoreSpace;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Evaluation
{
  public class StatisticsTests
  {
    [Fact]
    public void ShouldCalculateKestnerRatio()
    {
      var inputs = new List<double> { 2.0, 5.0, 15.0, 35.0, 20.0, 55.0, 150.0 };
      var logs = inputs
        .Select(o => Math.Log(o))
        .Where(o => double.IsInfinity(o) == false && ConversionManager.Equals(o, 0) == false)
        .ToList();

      var count = logs.Count;
      var deviation = 0.0;
      var regression = new double[count];
      var slope = Math.Log(inputs.Last()) / count;

      for (var i = 0; i < count; i++)
      {
        var v = inputs.ElementAtOrDefault(i);
        var log = Math.Log(v);

        if (double.IsNaN(log) == false)
        {
          regression[i] = regression.ElementAtOrDefault(i - 1) + slope;
          deviation += Math.Pow(regression[i] - log, 2);
        }
      }

      var error = Math.Sqrt(deviation / count) / Math.Sqrt(count);
      var expectation = slope / (error * count);
      var deals = inputs.Select((o, i) => new InputData { Value = o });
      var kestnerRatio = new KestnerRatio
      {
        Values = deals
      };

      var ratio = kestnerRatio.Calculate();

      Assert.Equal(expectation, ratio, 2);
    }

    [Fact]
    public void ShouldCalculateStandardScore()
    {
      var inputs = new List<double> { -2, -5, -15, 35, -20, 55, -150, 5, 25, 50 };
      var deals = inputs.Select(o => new InputData { Value = o });
      var scoreMetrics = new StandardScore { Values = deals };
      var score = scoreMetrics.Calculate();

      Assert.Equal(3.20, score, 2);

      inputs = new List<double> { -2, -5, -15, -35, -20, 55, 150, 5, 25, 50 };
      deals = inputs.Select(o => new InputData { Value = o });
      scoreMetrics = new StandardScore { Values = deals };
      score = scoreMetrics.Calculate();

      Assert.Equal(-3.375, score, 2);
    }

    [Fact]
    public void ShouldCalculateLinearRegressionCorrelation()
    {
      var inputs = new List<double> { 0, 1, 2, 3, 4, 5, 6, 7 };
      var deals = inputs.Select(o => new InputData { Value = o });
      var scoreMetrics = new RegressionCorrelation { Values = deals };
      var score = scoreMetrics.Calculate();

      Assert.Equal(1, score, 2);
    }
  }
}
