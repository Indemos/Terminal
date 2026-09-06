using Core.Extensions;
using Core.Models;
using System;
using System.Linq;

namespace Core.Groups
{
  public class TimeGroup : Group, IGroup
  {
    /// <summary>
    /// Time frame for grouping prices
    /// </summary>
    public virtual TimeSpan? TimeFrame { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Add price to the list
    /// </summary>
    /// <param name="nextPrice"></param>
    public override Price Update(Price nextPrice)
    {
      var currentPrice = Items.LastOrDefault() ?? new Price();
      var (price, expansion) = Group(currentPrice, nextPrice);

      if (expansion || Items.Count is 0)
      {
        Items.Add(price);
      }

      Items[^1] = price;

      return price;
    }

    /// <summary>
    /// Aggregate points
    /// </summary>
    /// <param name="currentPrice"></param>
    /// <param name="nextPrice"></param>
    /// <param name="span"></param>
    protected virtual (Price, bool) Group(Price currentPrice, Price nextPrice)
    {
      var nextTime = nextPrice.Time;
      var currentTime = currentPrice?.Bar?.Time ?? DateTime.MinValue.Ticks;
      var expansion = nextTime - currentTime >= TimeFrame.Value.Ticks;
      var sidePrice = nextPrice.Bid ?? nextPrice?.Ask;
      var price = (nextPrice.Last ?? currentPrice.Last ?? sidePrice).Value;

      if (expansion)
      {
        currentPrice = nextPrice;
      }

      var group = new Price
      {
        Last = price,
        Time = nextPrice.Time,
        Volume = nextPrice.Volume,
        Ask = nextPrice.Ask ?? currentPrice?.Ask ?? price,
        Bid = nextPrice.Bid ?? currentPrice?.Bid ?? price,
        AskSize = nextPrice.AskSize ?? currentPrice?.AskSize ?? 0.0,
        BidSize = nextPrice.BidSize ?? currentPrice?.BidSize ?? 0.0,
        Bar = new()
        {
          Close = price,
          Low = Math.Min(price, currentPrice?.Bar?.Low ?? price),
          High = Math.Max(price, currentPrice?.Bar?.High ?? price),
          Open = currentPrice?.Bar?.Open ?? price,
          Time = nextPrice.Time.Round(TimeFrame)
        }
      };

      return (group, expansion);
    }
  }
}
