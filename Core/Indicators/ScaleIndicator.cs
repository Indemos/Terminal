using Distribution.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using Terminal.Core.Domains;
using Terminal.Core.Extensions;
using Terminal.Core.Models;
using Terminal.Core.Services;

namespace Terminal.Core.Indicators
{
  public class ScaleIndicator : Indicator<ScaleIndicator>
  {
    /// <summary>
    /// Number of bars to average
    /// </summary>
    public int Interval { get; set; }

    /// <summary>
    /// Bottom border of the normalized series
    /// </summary>
    public double Min { get; set; }

    /// <summary>
    /// Top border of the normalized series
    /// </summary>
    public double Max { get; set; }

    /// <summary>
    /// Preserve last calculated min value
    /// </summary>
    protected double? min = null;

    /// <summary>
    /// Preserve last calculated max value
    /// </summary>
    protected double? max = null;

    /// <summary>
    /// Calculate indicator value
    /// </summary>
    /// <param name="collection"></param>
    public override ScaleIndicator Update(IList<PointModel> collection)
    {
      var currentPoint = collection.LastOrDefault();

      if (currentPoint is null)
      {
        return this;
      }

      var value = currentPoint.Last ?? 0.0;
      var interval = Math.Min(Interval, collection.Count);
      var comService = InstanceService<AverageService>.Instance;

      min = min is null ? value : Math.Min(min.Value, value);
      max = max is null ? value : Math.Max(max.Value, value);

      if (min.Value.Is(max.Value) is false)
      {
        value = Min + (value - min.Value) * (Max - Min) / (max.Value - min.Value);
      }

      Point.Last = comService.LinearWeightAverage(collection.Select(o => o.Last.Value), collection.Count - 1, interval);

      return this;
    }
  }
}
