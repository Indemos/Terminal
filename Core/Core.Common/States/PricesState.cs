using Core.Common.Enums;
using Orleans;
using System;
using System.Collections.Generic;

namespace Core.Common.States
{
  [Immutable]
  [GenerateSerializer]
  public record PricesState
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
    /// Reference to the instrument
    /// </summary>
    [Id(2)] public InstrumentState Instrument { get; init; }

    /// <summary>
    /// List of all ticks from the server
    /// </summary>
    [Id(3)] public List<PriceState> Prices { get; init; } = new();

    /// <summary>
    /// List of all ticks from the server aggregated into bars
    /// </summary>
    [Id(4)] public List<PriceState> PriceGroups { get; init; } = new();
  }
}
