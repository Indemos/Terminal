using Core.Models;
using System;
using System.Linq;

namespace Core.Groups
{
  public class RenkoGroup : Group, IGroup
  {
    public virtual double Size { get; set; } = 1.0;

    public virtual int Direction { get; protected set; }
    public virtual double Close { get; protected set; }
    public virtual double Price { get; protected set; }

    protected virtual Price Last => Items.LastOrDefault() ?? new Price();

    protected bool setup;

    /// <summary>
    /// Update
    /// </summary>
    /// <param name="nextPrice"></param>
    /// <returns></returns>
    public override Price Update(Price nextPrice)
    {
      var price = nextPrice?.Last ?? nextPrice?.Bid ?? nextPrice?.Ask;

      if (price is null || Size <= 0 || double.IsFinite(price.Value) is false)
      {
        return Last;
      }

      Price = price.Value;

      if (setup is false)
      {
        Close = price.Value;
        setup = true;

        return Last;
      }

      var response = Last;

      while (Step(Price, out var open, out var close, out var dir))
      {
        response = Box(nextPrice, open, close);
        Items.Add(response);
        Direction = dir;
        Close = close;
      }

      return response;
    }

    /// <summary>
    /// Get next bar
    /// </summary>
    /// <param name="price"></param>
    /// <param name="open"></param>
    /// <param name="close"></param>
    /// <param name="dir"></param>
    protected virtual bool Step(double price, out double open, out double close, out int dir)
    {
      open = Close;
      close = Close;
      dir = Direction;

      switch (Direction)
      {
        case 0:

          if (price >= Close + Size) { dir = 1; close = Close + Size; return true; }
          if (price <= Close - Size) { dir = -1; close = Close - Size; return true; }

          return false;

        case 1:

          if (price >= Close + Size) { close = Close + Size; return true; }
          if (price <= Close - 2 * Size) { open = Close - Size; close = Close - 2 * Size; dir = -1; return true; }

          return false;

        case -1:

          if (price <= Close - Size) { close = Close - Size; return true; }
          if (price >= Close + 2 * Size) { open = Close + Size; close = Close + 2 * Size; dir = 1; return true; }

          return false;
      }

      Direction = 0;

      return false;
    }

    /// <summary>
    /// For the bar
    /// </summary>
    /// <param name="price"></param>
    /// <param name="open"></param>
    /// <param name="close"></param>
    protected virtual Price Box(Price price, double open, double close) => new()
    {
      Ask = price.Ask,
      Bid = price.Bid,
      Last = close,
      Time = price.Time, 
      Volume = price.Volume ?? 0,
      AskSize = price.AskSize ?? 0,
      BidSize = price.BidSize ?? 0,
      Bar = new()
      {
        Open = open,
        Close = close,
        High = Math.Max(open, close),
        Low = Math.Min(open, close),
        Time = price.Time
      }
    };
  }
}
