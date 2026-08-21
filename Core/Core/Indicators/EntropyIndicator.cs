using System;
using System.Collections.Generic;

namespace Core.Indicators
{
  /// <summary>
  /// Shannon Entropy Indicator based on price direction.
  /// </summary>
  public class EntropyIndicator
  {
    /// <summary>
    /// Number of observations used for calculation.
    /// </summary>
    public virtual int Period { get; set; } = 15;

    /// <summary>
    /// Shannon entropy of price directions.
    /// </summary>
    /// <param name="items">Price observations.</param>
    /// <returns>Entropy in the range [0, 1].</returns>
    public virtual double Update(List<double?> items)
    {
      var period = Math.Min(Period, items.Count);

      if (period < 2) return 0;

      var entropy = 0.0;
      var count = period - 1;
      var directions = new int[3]; // 0 = down, 1 = even, 2 = up

      for (var i = items.Count - period + 1; i < items.Count; i++)
      {
        directions[Math.Sign((items[i] - items[i - 1]).Value) + 1]++;
      }

      for (var i = 0; i < directions.Length; i++)
      {
        if (directions[i] > 0)
        {
          entropy -= (directions[i] / count) * Math.Log2(directions[i] / count);
        }
      }

      // Normalize [0, 1]

      return entropy / Math.Log2(directions.Length);
    }
  }
}
