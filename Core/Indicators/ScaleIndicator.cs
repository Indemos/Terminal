using System;
using System.Collections.Generic;
using System.Linq;
using Terminal.Core.Domains;
using Terminal.Core.Extensions;
using Terminal.Core.Models;

namespace Terminal.Core.Indicators
{
  public class ScaleIndicator : Indicator<ScaleIndicator>
  {
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

      min = Math.Min(min ?? value, value);
      max = Math.Max(max ?? value, value);

      switch (min.Value.Is(max.Value))
      {
        case true: value = (Max + Min) / 2.0; break;
        case false: value = Min + (Max - Min) * (value - min.Value) / (max.Value - min.Value); break;
      }

      Point.Last = value;

      return this;
    }
  }
}
