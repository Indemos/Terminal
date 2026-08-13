using Core.Extensions;
using Core.Models;
using System;

namespace Core.Indicators
{
  public class ScaleIndicator
  {
    /// <summary>
    /// Bottom border of the normalized series
    /// </summary>
    public virtual double Min { get; set; }

    /// <summary>
    /// Top border of the normalized series
    /// </summary>
    public virtual double Max { get; set; }

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
    /// <param name="items"></param>
    public virtual double Update(Price currentPoint)
    {
      var value = currentPoint.Last ?? 0.0;

      min = Math.Min(min ?? value, value);
      max = Math.Max(max ?? value, value);

      switch (min.Is(max.Value))
      {
        case true: value = (Max + Min) / 2.0; break;
        case false: value = Min + (Max - Min) * (value - min.Value) / (max.Value - min.Value); break;
      }

      return value;
    }
  }
}
