using Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.Indicators
{
  public enum ImbalanceMode
  {
    Delta,
    Volume
  }

  /// <summary>
  /// Order Imbalance Indicator
  /// </summary>
  public class ImbalanceIndicator
  {
    public ImbalanceMode Mode { get; set; } = ImbalanceMode.Volume;

    /// <summary>
    /// Calculate
    /// </summary>
    /// <param name="dom"></param>
    /// <param name="count"></param>
    public virtual double? Update(Dom dom, int? count)
    {
      if (dom is null)
      {
        return 0;
      }

      var bids = Sum(dom.Bids, count ?? 0);
      var asks = Sum(dom.Asks, count ?? 0);

      switch (Mode)
      {
        case ImbalanceMode.Volume: return bids + asks;
        case ImbalanceMode.Delta: return (bids - asks) / (bids + asks + 1e-9);
      }

      return null;
    }

    /// <summary>
    /// Get weigthed sum of all order sizes at selected price lvels
    /// </summary>
    /// <param name="prices"></param>
    /// <param name="count"></param>
    protected virtual double Sum(IEnumerable<KeyValuePair<long, LinkedList<DomOrder>>> prices, int count)
    {
      var cnt = Math.Min(prices.Count(), count);

      return prices
        .Take(cnt)
        .Select((o, i) => o.Value.Sum(o => o.Size ?? 0) * (cnt - i))
        .Sum();
    }
  }
}
