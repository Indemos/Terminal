using Core.Conventions;
using Core.Extensions;
using Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Indicators
{
  public class ScaleIndicator : Indicator
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
    /// <param name="prices"></param>
    public override Task<IIndicator> Update(IList<PriceModel> prices)
    {
      var response = Task.FromResult<IIndicator>(this);
      var currentPoint = prices.LastOrDefault();

      if (currentPoint is null)
      {
        return response;
      }

      var value = currentPoint.Last ?? 0.0;

      min = Math.Min(min ?? value, value);
      max = Math.Max(max ?? value, value);

      switch (min.Is(max.Value))
      {
        case true: value = (Max + Min) / 2.0; break;
        case false: value = Min + (Max - Min) * (value - min.Value) / (max.Value - min.Value); break;
      }

      Response = Response with { Last = value };

      return response;
    }
  }
}
