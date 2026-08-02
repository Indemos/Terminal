using Core.Conventions;
using Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

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
    public override IIndicator Update(IList<Price> collection)
    {
      var response = this;
      var currentPoint = collection.ElementAtOrDefault(collection.Count - 1);
      var previousPoint = collection.ElementAtOrDefault(collection.Count - 2);

      if (currentPoint is null || previousPoint is null)
      {
        return response;
      }

      var interval = Math.Max(Math.Min(Interval, collection.Count), 1);

      var max = new double[]
      {
        Math.Abs(currentPoint.Bar.High.Value - previousPoint.Bar.Close.Value),
        Math.Abs(currentPoint.Bar.Low.Value - previousPoint.Bar.Close.Value),
        currentPoint.Bar.High.Value - currentPoint.Bar.Low.Value

      }.Max();

      var value = ((Response.Last ?? 1) * Math.Max(interval - 1, 0) + max) / interval;

      Response = Response with { Last = value };

      return response;
    }
  }
}
