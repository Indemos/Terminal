using Core.Models;
using System;
using System.Linq;

namespace Core.Groups
{
  public class VolumeGroup : Group, IGroup
  {
    public virtual double TargetVolume { get; set; } = 1000.0;
    public virtual int MaxBars { get; set; } = 100000;
    public virtual Price Last => Items.LastOrDefault() ?? new Price();

    /// <summary>
    /// Update
    /// </summary>
    /// <param name="p"></param>
    /// <returns></returns>
    public override Price Update(Price p)
    {
      var volume = p.Volume ?? 0;
      var price = (p.Last ?? p.Bid ?? p.Ask).Value;

      if (!double.IsFinite(volume) || volume < 0)
      {
        volume = 0;
      }

      if (Items.Count is 0 || Items[^1].Bar is null)
      {
        Items.Add(Create(p, price, price, price, price, 0));
      }

      var current = Items[^1];

      if (volume <= 0)
      {
        return Items[^1] = State(current, p, price);
      }

      var remaining = volume;
      var created = 0;

      while (remaining > 0)
      {
        var cur = current.Volume ?? 0;
        var cap = TargetVolume - cur;

        if (cap <= 0)
        {
          if (++created > MaxBars) break;
          current = Create(p, price, price, price, price, 0);
          Items.Add(current);
          continue;
        }

        var used = Math.Min(remaining, cap);

        current = Trade(current, p, price, used);
        remaining -= used;

        if (current.Volume >= TargetVolume)
        {
          current = current with { Volume = TargetVolume };
          Items[^1] = current;

          if (remaining <= 0)
          {
            Items.Add(Create(p, price, price, price, price, 0));
            break;
          }

          if (++created > MaxBars)
          {
            Items.Add(Create(p, price, price, price, price, remaining));
            remaining = 0;
            break;
          }

          current = Create(p, price, price, price, price, 0);
          Items.Add(current);
        }
      }

      current = State(Items[^1], p, price);
      Items[^1] = current;

      return current;
    }

    protected virtual Price Trade(Price price, Price nextPrice, double pr, double v) => price with
    {
      Last = pr,
      Time = nextPrice.Time,
      Volume = (price.Volume ?? 0) + v,
      Ask = nextPrice.Ask ?? price.Ask ?? pr,
      Bid = nextPrice.Bid ?? price.Bid ?? pr,
      AskSize = nextPrice.AskSize ?? price.AskSize ?? 0,
      BidSize = nextPrice.BidSize ?? price.BidSize ?? 0,
      Bar = price.Bar with
      {
        Close = pr,
        Low = Math.Min(price.Bar.Low ?? pr, pr),
        High = Math.Max(price.Bar.High ?? pr, pr)
      }
    };

    protected virtual Price State(Price price, Price nextPrice, double pr) => price with
    {
      Last = pr,
      Time = nextPrice.Time,
      Ask = nextPrice.Ask ?? price.Ask ?? pr,
      Bid = nextPrice.Bid ?? price.Bid ?? pr,
      AskSize = nextPrice.AskSize ?? price.AskSize ?? 0,
      BidSize = nextPrice.BidSize ?? price.BidSize ?? 0,
      Bar = price.Bar with
      {
        Close = pr,
        Low = Math.Min(price.Bar.Low ?? pr, pr),
        High = Math.Max(price.Bar.High ?? pr, pr)
      }
    };

    protected virtual Price Create(Price price, double O, double H, double L, double C, double V) => new()
    {
      Last = C,
      Volume = V,
      Time = price.Time,
      Ask = price.Ask ?? C,
      Bid = price.Bid ?? C,
      AskSize = price.AskSize ?? 0,
      BidSize = price.BidSize ?? 0,
      Bar = new()
      {
        Low = L,
        High = H,
        Open = O,
        Close = C,
        Time = price.Time
      }
    };
  }
}
