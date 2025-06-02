using Distribution.Services;
using System.Collections.Generic;
using System.Linq;
using Terminal.Core.Domains;
using Terminal.Core.Extensions;
using Terminal.Core.Models;
using Terminal.Core.Services;

namespace Terminal.Core.Indicators
{
  /// <summary>
  /// Calculation mode
  /// </summary>
  public enum AveragePriceEnum : byte
  {
    Bid = 1,
    Ask = 2,
    Close = 3
  }

  public class MaIndicator : Indicator<MaIndicator>
  {
    /// <summary>
    /// Number of bars to average
    /// </summary>
    public int Interval { get; set; }

    /// <summary>
    /// Calculation mode
    /// </summary>
    public AveragePriceEnum Mode { get; set; }

    /// <summary>
    /// Calculate single value
    /// </summary>
    /// <param name="collection"></param>
    /// <returns></returns>
    public override MaIndicator Update(IList<PointModel> collection)
    {
      var currentPoint = collection.LastOrDefault();

      if (currentPoint is null)
      {
        return this;
      }

      var value = currentPoint.Last.Value;
      var comService = InstanceService<AverageService>.Instance;

      switch (Mode)
      {
        case AveragePriceEnum.Bid: value = currentPoint.Bid.Value; break;
        case AveragePriceEnum.Ask: value = currentPoint.Ask.Value; break;
      }

      var average = comService.LinearWeightAverage(collection.Select(o => o.Last.Value), collection.Count - 1, Interval);

      Point.Last = average.Is(0) ? value : average;

      return this;
    }
  }
}
