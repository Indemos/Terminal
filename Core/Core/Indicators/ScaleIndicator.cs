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
    /// Smoothing EWMA factor for min / max calculation
    /// 1 - no smoothing
    /// 0 - max smoothing
    /// </summary>
    public virtual double Memory { get; set; } = 1;

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
    /// <param name="currentPoint"></param>
    public virtual double Update(Price currentPoint)
    {
      var value = currentPoint.Last ?? 0.0;

      if (min is null)
      {
        min = value;
        max = value;
      }
      else
      {
        min = value < min.Value ? value : min.Value + (1.0 - Memory) * (value - min.Value);
        max = value > max.Value ? value : max.Value + (1.0 - Memory) * (value - max.Value);
      }

      var scale = Math.Max(Math.Abs(min.Value), Math.Abs(max.Value));

      return scale is 0 ? (Min + Max) / 2.0 : value / scale;
    }
  }
}
