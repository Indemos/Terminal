using System;
using System.Collections.Generic;
using System.Linq;
using Terminal.Core.Domains;
using Terminal.Core.Models;

namespace Terminal.Core.Indicators
{
  public class AtrIndicator : Indicator<AtrIndicator>
  {
    /// <summary>
    /// Number of bars to average
    /// </summary>
    public int Interval { get; set; }

    /// <summary>
    /// Calculate single value
    /// </summary>
    /// <param name="collection"></param>
    /// <returns></returns>
    public override AtrIndicator Update(IList<PointModel> collection)
    {
      var currentPoint = collection.ElementAtOrDefault(collection.Count - 1);
      var previousPoint = collection.ElementAtOrDefault(collection.Count - 2);

      if (currentPoint is null || previousPoint is null)
      {
        return this;
      }

      var value =
        Math.Max(currentPoint.Bar.High.Value, previousPoint.Bar.Close.Value) -
        Math.Min(currentPoint.Bar.Low.Value, previousPoint.Bar.Close.Value);

      if (collection.Count > Interval)
      {
        value = (currentPoint.Series[Name].Last.Value * Math.Max(Interval - 1, 0) + value) / Interval;
      }

      Point.Last = value;

      currentPoint.Series[Name] = Point;

      return this;
    }
  }
}
