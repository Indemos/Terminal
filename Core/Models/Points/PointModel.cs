using System;
using System.Collections.Generic;
using Terminal.Core.Collections;
using Terminal.Core.Domains;
using Terminal.Core.Extensions;

namespace Terminal.Core.Models
{
  public class PointModel : ICloneable, IGroup<PointModel>
  {
    /// <summary>
    /// Bid
    /// </summary>
    public virtual double? Bid { get; set; }

    /// <summary>
    /// Ask
    /// </summary>
    public virtual double? Ask { get; set; }

    /// <summary>
    /// Volume of the bid 
    /// </summary>
    public virtual double? BidSize { get; set; }

    /// <summary>
    /// Volume of the ask
    /// </summary>
    public virtual double? AskSize { get; set; }

    /// <summary>
    /// Last price or value
    /// </summary>
    public virtual double? Last { get; set; }

    /// <summary>
    /// Instrument volume
    /// </summary>
    public virtual double? Volume { get; set; }

    /// <summary>
    /// Time stamp
    /// </summary>
    public virtual DateTime? Time { get; set; }

    /// <summary>
    /// Aggregation period for the quotes
    /// </summary>
    public virtual TimeSpan? TimeFrame { get; set; }

    /// <summary>
    /// Reference to the complex data point
    /// </summary>
    public virtual BarModel Bar { get; set; }

    /// <summary>
    /// Reference to the instrument
    /// </summary>
    public virtual InstrumentModel Instrument { get; set; }

    /// <summary>
    /// Indicator values calculated for the current data point
    /// </summary>
    public virtual IDictionary<string, PointModel> Series { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public PointModel()
    {
      Series = new Dictionary<string, PointModel>();
    }

    /// <summary>
    /// Clone
    /// </summary>
    /// <returns></returns>
    public virtual object Clone()
    {
      var clone = MemberwiseClone() as PointModel;

      clone.Bar = Bar?.Clone() as BarModel;

      return clone;
    }

    /// <summary>
    /// Grouping index
    /// </summary>
    /// <returns></returns>
    public virtual long GetIndex()
    {
      if (TimeFrame is not null)
      {
        return Time.Round(TimeFrame).Value.Ticks;
      }

      return Time.Value.Ticks;
    }

    /// <summary>
    /// Grouping implementation
    /// </summary>
    /// <param name="o"></param>
    /// <returns></returns>
    public virtual PointModel Update(PointModel o)
    {
      var currentPrice = Last;
      var previousPrice = o?.Last;
      var price = (currentPrice ?? previousPrice).Value;
      var min = Math.Min(Bar?.Low ?? price, o?.Bar?.Low ?? price);
      var max = Math.Max(Bar?.High ?? price, o?.Bar?.High ?? price);

      Ask ??= o?.Ask ?? price;
      Bid ??= o?.Bid ?? price;
      AskSize ??= o?.AskSize ?? 0.0;
      BidSize ??= o?.BidSize ?? 0.0;
      Bar ??= new BarModel();
      Bar.Close = Last = price;
      Bar.Low = Math.Min(min, price);
      Bar.High = Math.Max(max, price);
      Bar.Open = Bar.Open ?? o?.Bar?.Open ?? price;
      Time = Time.Round(TimeFrame) ?? o?.Time;

      return this;
    }
  }
}
