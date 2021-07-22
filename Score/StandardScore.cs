using System;
using System.Collections.Generic;
using System.Linq;

namespace ScoreSpace
{
  /// <summary>
  /// Standard Z-Score 
  /// A probability of the losing or winning strikes.
  /// Negative value means the sign of the next deal usually changes, i.e from loss to profit and vice versa.
  /// Positive value means the deal will have the same sign as previous, i.e. loss after loss and profit after profit.
  /// Z = Standard score
  /// N = Total number of observations
  /// R = Total number of losing and winning series
  /// W = Number of winning deals
  /// L = Number of losing deals
  /// P = 2 * W * L
  /// Z = (N * (R - 0.5) - P) / ((P * (P - N)) / (N - 1)) ^ (1 / 2)
  /// </summary>
  public class StandardScore
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
      var gains = 0;
      var losses = 0;
      var seriesGains = 0;
      var seriesLosses = 0;
      var seriesInverse = 0;

      for (var i = 1; i < count; i++)
      {
        var currentValue = Values.ElementAtOrDefault(i)?.Value ?? 0.0;
        var previousValue = Values.ElementAtOrDefault(i - 1)?.Value ?? 0.0;
        var direction = previousValue > currentValue ? -1 : 1;

        switch (direction)
        {
          case 1: gains++; break;
          case -1: losses++; break;
        }

        if (seriesInverse != 0)
        {
          seriesGains += seriesInverse > direction ? 1 : 0;
          seriesLosses += seriesInverse < direction ? 1 : 0;
        }

        seriesInverse = direction;
      }

      var dealsCount = gains + losses;
      var seriesCount = 2 * seriesGains * seriesLosses;
      var divisor = seriesCount * (seriesCount - count) / (count - 1.0);

      if (divisor == 0)
      {
        return 0.0;
      }

      return Math.Sqrt(count * (dealsCount - 0.5) - seriesCount) / divisor;
    }
  }
}
