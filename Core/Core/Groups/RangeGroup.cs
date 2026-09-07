using Core.Models;
using System;
using System.Linq;

namespace Core.Groups
{
  public class RangeGroup : Group, IGroup
  {
    /// <summary>
    /// Size of each bar
    /// </summary>
    public virtual double Size { get; set; } = 1.0;

    /// <summary>
    /// Max bars per update
    /// </summary>
    public virtual int MaxBars { get; set; } = 100000;

    /// <summary>
    /// Last range price
    /// </summary>
    public virtual Price Last => Items.LastOrDefault() ?? new Price();

    /// <summary>
    /// Update
    /// </summary>
    /// <param name="p"></param>
    /// <returns></returns>
    public override Price Update(Price p)
    {
      var price = (p.Last ?? p.Bid ?? p.Ask).Value;
      var volume = double.IsFinite(p.Volume ?? 0) ? p.Volume ?? 0 : 0;

      if (Items.Count is 0 || Items[^1].Bar is null)
      {
        var box = Create(p, price, price, price, price, volume);
        Items.Add(box);
        return box;
      }

      var curBox = Items[^1];
      var O = curBox.Bar.Open.Value;
      var H = curBox.Bar.High.Value;
      var L = curBox.Bar.Low.Value;
      var prevH = H;
      var prevL = L;

      H = Math.Max(H, price);
      L = Math.Min(L, price);

      var created = 0;

      while (H - L >= Size)
      {
        if (++created > MaxBars) break;

        var close = price switch
        {
          var o when o > prevH => L + Size,
          var o when o < prevL => H - Size,
          var o when o >= O => L + Size,
          _ => H - Size
        };

        Items[^1] = Complete(curBox, O, close, p.Time.Value);

        O = close;
        H = close;
        L = close;
        H = Math.Max(H, price);
        L = Math.Min(L, price);

        curBox = Create(p, O, H, L, price, 0);
        prevH = H;
        prevL = L;

        Items.Add(curBox);
      }

      curBox = curBox with
      {
        Last = price,
        Time = p.Time,
        Ask = p.Ask ?? curBox.Ask,
        Bid = p.Bid ?? curBox.Bid,
        Volume = (curBox.Volume ?? 0) + volume,
        AskSize = p.AskSize ?? curBox.AskSize,
        BidSize = p.BidSize ?? curBox.BidSize,
        Bar = curBox.Bar with
        {
          Close = price,
          Low = Math.Min(curBox.Bar.Low.Value, price),
          High = Math.Max(curBox.Bar.High.Value, price)
        }
      };

      Items[^1] = curBox;

      return curBox;
    }

    /// <summary>
    /// Create a new price bar
    /// </summary>
    /// <param name="price"></param>
    /// <param name="O"></param>
    /// <param name="H"></param>
    /// <param name="L"></param>
    /// <param name="C"></param>
    /// <param name="V"></param>
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

    /// <summary>
    /// Complete the current price bar
    /// </summary>
    /// <param name="price"></param>
    /// <param name="O"></param>
    /// <param name="C"></param>
    /// <param name="T"></param>
    protected virtual Price Complete(Price price, double O, double C, long T)
    {
      var H = C >= O ? C : C + Size;
      var L = C >= O ? C - Size : C;

      return price with
      {
        Last = C,
        Time = T,
        Bar = price.Bar with
        {
          Low = L,
          High = H,
          Open = O,
          Close = C,
          Time = price.Bar.Time
        }
      };
    }
  }
}
