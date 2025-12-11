using Core.Conventions;
using Core.Extensions;
using Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Indicators
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

  public class MaIndicator : Indicator
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
    public override Task<IIndicator> Update(IList<Price> collection)
    {
      var response = Task.FromResult<IIndicator>(this);
      var currentPoint = collection.LastOrDefault();

      if (currentPoint is null)
      {
        return response;
      }

      var value = currentPoint.Last.Value;

      switch (Mode)
      {
        case AveragePriceEnum.Bid: value = currentPoint.Bid.Value; break;
        case AveragePriceEnum.Ask: value = currentPoint.Ask.Value; break;
      }

      var interval = Math.Min(Interval, collection.Count);
      var average = Average.LinearWeightAverage([.. collection.Select(o => o.Last.Value)], collection.Count - 1, interval) as double?;

      Response = Response with { Last = average.Is(0) ? value : average };

      return response;
    }
  }
}
