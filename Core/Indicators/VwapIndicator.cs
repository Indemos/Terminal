using System;
using System.Collections.Generic;
using System.Linq;
using Terminal.Core.Domains;
using Terminal.Core.Models;

namespace Terminal.Core.Indicators
{
  public class VwapIndicator : Indicator<VwapIndicator>
  {
    /// <summary>
    /// Calculate indicator value
    /// </summary>
    /// <param name="collection"></param>
    public override VwapIndicator Update(IList<PointModel> collection)
    {
      var currentPoint = collection.LastOrDefault();

      if (currentPoint?.Bar is null)
      {
        return this;
      }

      var cumPrice = 0.0;
      var cumVolume = 0.0;
      var items = new List<(double Price, double Volume)>();

      Point.Bar ??= new BarModel();

      foreach (var point in collection)
      {
        var price = (point.Bar.Low + point.Bar.High + point.Bar.Close).Value / 3.0;
        var volume = point.Volume ?? (point.BidSize ?? 0 + point.AskSize ?? 0);

        cumPrice += price * volume;
        cumVolume += volume;

        items.Add((price, volume));

        var average = cumPrice / cumVolume;
        var variance = items
            .Select(o => o.Volume * (o.Price - average) * (o.Price - average))
            .Sum() / cumVolume;

        var deviation = Math.Sqrt(variance);

        Point.Last = average;
        Point.Bar.High = average + 2.0 * deviation;
        Point.Bar.Low = average - 2.0 * deviation;
      }

      currentPoint.Series[Name] = Point.Clone() as PointModel;

      return this;
    }
  }
}
