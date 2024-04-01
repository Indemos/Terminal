using System;
using System.Collections.Generic;
using Terminal.Core.Domains;

namespace Terminal.Core.Models
{
  public struct PointModel
  {
    /// <summary>
    /// Bid
    /// </summary>
    public double? Bid { get; set; }

    /// <summary>
    /// Ask
    /// </summary>
    public double? Ask { get; set; }

    /// <summary>
    /// Volume of the bid 
    /// </summary>
    public double? BidSize { get; set; }

    /// <summary>
    /// Volume of the ask
    /// </summary>
    public double? AskSize { get; set; }

    /// <summary>
    /// Last price or value
    /// </summary>
    public double? Price { get; set; }

    /// <summary>
    /// Time stamp
    /// </summary>
    public DateTime? Time { get; set; }

    /// <summary>
    /// Aggregation period for the quotes
    /// </summary>
    public TimeSpan? TimeFrame { get; set; }

    /// <summary>
    /// Reference to the complex data point
    /// </summary>
    public BarModel? Bar { get; set; }

    /// <summary>
    /// Reference to the instrument
    /// </summary>
    public IInstrument Instrument { get; set; }

    /// <summary>
    /// Values from related series synced with the current data point, e.g. averaged indicator calculations for the charts
    /// </summary>
    public IDictionary<string, PointModel> Series { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public PointModel()
    {
      Time = DateTime.Now;

      Series = new Dictionary<string, PointModel>();
    }
  }
}
