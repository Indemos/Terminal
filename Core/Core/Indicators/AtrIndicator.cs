using Core.Conventions;
using Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Indicators
{
  public class AtrIndicator : Indicator
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
      var currentPoint = collection.ElementAtOrDefault(collection.Count - 1);
      var previousPoint = collection.ElementAtOrDefault(collection.Count - 2);

      if (currentPoint is null || previousPoint is null)
      {
        return response;
      }

      var interval = Math.Min(Interval, collection.Count);
      var value =
        Math.Max(currentPoint.Bar.High.Value, previousPoint.Bar.Close.Value) -
        Math.Min(currentPoint.Bar.Low.Value, previousPoint.Bar.Close.Value);

      if (interval is not 0)
      {
        value = ((Response.Last ?? 1) * Math.Max(Interval - 1, 0) + value) / interval;
      }

      Response = Response with { Last = value };

      return response;
    }
  }
}
