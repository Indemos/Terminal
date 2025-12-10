using Core.Conventions;
using Core.Extensions;
using Core.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Indicators
{
  public class RsiIndicator : Indicator
  {
    /// <summary>
    /// Number of bars to average
    /// </summary>
    public int Interval { get; set; }

    /// <summary>
    /// Calculate single value
    /// </summary>
    /// <param name="collection"></param>
    public override Task<IIndicator> Update(IList<Price> collection)
    {
      var response = Task.FromResult<IIndicator>(this);
      var currentPoint = collection.LastOrDefault();

      if (currentPoint is null)
      {
        return response;
      }

      var ups = new List<double>(Interval);
      var downs = new List<double>(Interval);

      for (var i = 1; i <= Interval; i++)
      {
        var nextPrice = collection.ElementAtOrDefault(collection.Count - i);
        var previousPrice = collection.ElementAtOrDefault(collection.Count - i - 1);

        if (nextPrice is not null && previousPrice is not null)
        {
          ups.Add(Math.Max(nextPrice.Last.Value - previousPrice.Last.Value, 0.0));
          downs.Add(Math.Max(previousPrice.Last.Value - nextPrice.Last.Value, 0.0));
        }
      }

      var interval = Math.Min(Interval, collection.Count);
      var averageUp = Average.SimpleAverage(ups, ups.Count - 1, interval);
      var averageDown = Average.SimpleAverage(downs, downs.Count - 1, interval) as double?;
      var average = averageDown.Is(0) ? 1.0 : averageUp / averageDown;

      Response = Response with { Last = 100.0 - 100.0 / (1.0 + average) };

      return response;
    }
  }
}
