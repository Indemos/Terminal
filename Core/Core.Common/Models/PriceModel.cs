using System;

namespace Core.Common.Models
{
  public record PriceModel
  {
    /// <summary>
    /// Name
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Bid
    /// </summary>
    public double? Bid { get; init; }

    /// <summary>
    /// Ask
    /// </summary>
    public double? Ask { get; init; }

    /// <summary>
    /// Volume of the bid 
    /// </summary>
    public double? BidSize { get; init; }

    /// <summary>
    /// Volume of the ask
    /// </summary>
    public double? AskSize { get; init; }

    /// <summary>
    /// Last price or value
    /// </summary>
    public double? Last { get; init; }

    /// <summary>
    /// Instrument volume
    /// </summary>
    public double? Volume { get; init; }

    /// <summary>
    /// Time stamp
    /// </summary>
    public long? Time { get; init; }

    /// <summary>
    /// Aggregation period for the quotes
    /// </summary>
    public TimeSpan? TimeFrame { get; init; }

    /// <summary>
    /// Reference to the complex data point
    /// </summary>
    public BarModel Bar { get; init; }
  }
}
