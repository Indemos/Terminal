using Core.Common.States;
using System;
using System.Collections.Generic;

namespace Simulation.States
{
  public record SummaryState
  {
    /// <summary>
    /// Aggregation period for the quotes
    /// </summary>
    public TimeSpan? TimeFrame { get; init; }

    /// <summary>
    /// Depth of market
    /// </summary>
    public DomState Dom { get; init; }

    /// <summary>
    /// Reference to the instrument
    /// </summary>
    public InstrumentState Instrument { get; init; }

    /// <summary>
    /// List of all ticks from the server
    /// </summary>
    public List<PriceState> Points { get; init; } = [];

    /// <summary>
    /// List of all ticks from the server aggregated into bars
    /// </summary>
    public List<PriceState> PointGroups { get; init; } = [];

    /// <summary>
    /// List of option contracts for the current point
    /// </summary>
    public List<InstrumentState> Options { get; init; } = [];
  }
}
