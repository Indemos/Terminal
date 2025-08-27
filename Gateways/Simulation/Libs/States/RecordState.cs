using Core.Common.Enums;
using Core.Common.States;
using Orleans;
using System;
using System.Collections.Generic;

namespace Simulation.States
{
  [Immutable]
  [GenerateSerializer]
  public record RecordState
  {
    /// <summary>
    /// Aggregation period for the quotes
    /// </summary>
    [Id(0)] public TimeSpan? TimeFrame { get; init; }

    /// <summary>
    /// Status
    /// </summary>
    [Id(1)] public StatusEnum? Status { get; init; }

    /// <summary>
    /// Depth of market
    /// </summary>
    [Id(2)] public DomState Dom { get; init; }

    /// <summary>
    /// Reference to the instrument
    /// </summary>
    [Id(3)] public InstrumentState Instrument { get; init; }

    /// <summary>
    /// List of option contracts for the current point
    /// </summary>
    [Id(4)] public List<InstrumentState> Options { get; init; } = new();

    /// <summary>
    /// List of all ticks from the server
    /// </summary>
    [Id(5)] public List<PriceState> Points { get; init; } = new();

    /// <summary>
    /// List of all ticks from the server aggregated into bars
    /// </summary>
    [Id(6)] public List<PriceState> PointGroups { get; init; } = new();
  }
}
