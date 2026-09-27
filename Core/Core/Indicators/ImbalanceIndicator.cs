using Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.Indicators
{
  public enum ImbalanceMode
  {
    Ratio,
    Volume,
    MicroPrice
  }

  /// <summary>
  /// Order Imbalance Indicator
  /// </summary>
  public class ImbalanceIndicator
  {
    /// <summary>
    /// Mode
    /// </summary>
    public ImbalanceMode Mode { get; set; } = ImbalanceMode.MicroPrice;

    /// <summary>
    /// Current value
    /// </summary>
    public double? Value { get; protected set; }

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
        case ImbalanceMode.Volume: return Value = bids + asks;
        case ImbalanceMode.Ratio: return Value = (bids - asks) / (bids + asks + 1e-9);
      }

      var bestBid = dom.Bids.First();
      var bestAsk = dom.Asks.First();

      return Value = (bids * bestAsk.Key + asks * bestBid.Key) / (bids + asks + 1e-9);
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
