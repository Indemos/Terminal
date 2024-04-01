using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Terminal.Core.Domains;
using Terminal.Core.Extensions;
using Terminal.Core.Models;

namespace Terminal.Core.Indicators
{
    /// <summary>
    /// Implementation
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class AverageTrueRangeIndicator : Indicator<AverageTrueRangeIndicator>
  {
    /// <summary>
    /// Number of bars to average
    /// </summary>
    public int Interval { get; set; }

    /// <summary>
    /// Preserve last calculated value
    /// </summary>
    public IList<double> Values { get; protected set; } = new List<double>();

    /// <summary>
    /// Calculate single value
    /// </summary>
    /// <param name="collection"></param>
    /// <returns></returns>
    public override AverageTrueRangeIndicator Calculate(ObservableCollection<PointModel?> collection)
    {
      var currentPoint = collection.ElementAtOrDefault(collection.Count - 1);
      var previousPoint = collection.ElementAtOrDefault(collection.Count - 2);

      if (currentPoint is null || previousPoint is null)
      {
        return this;
      }

      var pointLow = currentPoint?.Bar?.Low ?? 0;
      var pointHigh = currentPoint?.Bar?.High ?? 0;
      var pointClose = currentPoint?.Bar?.Close ?? 0;
      var value = Math.Max(pointHigh, pointClose) - Math.Min(pointLow, pointClose);

      if (Values.Count > Interval)
      {
        value = (Values.Last() * Math.Max(Interval - 1, 0) + value) / Interval; 
      }

      switch (Values.Count < collection.Count)
      {
        case true: Values.Add(value); break;
        case false: Values[collection.Count - 1] = value; break;
      }

      var point = Point ?? new PointModel();

      point.Price = value;
      Point = point;

      return this;
    }
  }
}
