using Orleans;
using System;
using System.Collections.Generic;

namespace Core.Common.States
{
  [Immutable]
  [GenerateSerializer]
  public record PriceState
  {
    /// <summary>
    /// Bid
    /// </summary>
    [Id(0)] public double? Bid { get; init; }

    /// <summary>
    /// Ask
    /// </summary>
    [Id(1)] public double? Ask { get; init; }

    /// <summary>
    /// Volume of the bid 
    /// </summary>
    [Id(2)] public double? BidSize { get; init; }

    /// <summary>
    /// Volume of the ask
    /// </summary>
    [Id(3)] public double? AskSize { get; init; }

    /// <summary>
    /// Last price or value
    /// </summary>
    [Id(4)] public double? Last { get; init; }

    /// <summary>
    /// Instrument volume
    /// </summary>
    [Id(5)] public double? Volume { get; init; }

    /// <summary>
    /// Time stamp
    /// </summary>
    [Id(6)] public DateTime? Time { get; init; }

    /// <summary>
    /// Aggregation period for the quotes
    /// </summary>
    [Id(7)] public TimeSpan? TimeFrame { get; init; }

    /// <summary>
    /// Reference to the complex data point
    /// </summary>
    [Id(8)] public BarState Bar { get; init; }

    /// <summary>
    /// Reference to the instrument
    /// </summary>
    [Id(9)] public InstrumentState Instrument { get; init; }

    /// <summary>
    /// Indicator values calculated for the current data point
    /// </summary>
    [Id(10)] public Dictionary<string, PriceState> Series { get; init; } = new();
  }
}
