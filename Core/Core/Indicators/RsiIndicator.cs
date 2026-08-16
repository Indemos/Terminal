using Core.Extensions;
using Core.Models;
using System;
using System.Collections.Generic;

namespace Core.Indicators
{
  public class RsiIndicator
  {
    /// <summary>
    /// Number of bars to average
    /// </summary>
    public int Period { get; set; }

    /// <summary>
    /// Calculate single value
    /// </summary>
    public virtual double? Update(IList<Price> collection)
    {
      var count = Math.Min(Period, collection.Count - 1);

      if (count <= 0)
      {
        return 50.0;
      }

      double? sumUp = 0.0;
      double? sumDown = 0.0;

      for (var i = 0; i < count; i++)
      {
        var current = collection[collection.Count - 1 - i].Last;
        var previous = collection[collection.Count - 2 - i].Last;
        var change = current - previous;

        switch (change)
        {
          case > 0: sumUp += change; break;
          case < 0: sumDown -= change; break;
        }
      }

      var averageUp = sumUp / count;
      var averageDown = sumDown / count;

      return averageDown.Is(0) ?
        averageUp.Is(0) ? 50.0 : 100.0 :
        100.0 - 100.0 / (1.0 + averageUp / averageDown);
    }
  }
}
