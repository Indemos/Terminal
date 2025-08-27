using Orleans;
using System;
using System.Collections.Generic;

namespace Core.Common.States
{
  [Immutable]
  [GenerateSerializer]
  public record ConditionState
  {
    /// <summary>
    /// Count
    /// </summary>
    [Id(0)] public int? Span { get; init; }

    /// <summary>
    /// Start date
    /// </summary>
    [Id(1)] public double? MinPrice { get; init; }

    /// <summary>
    /// End date
    /// </summary>
    [Id(2)] public double? MaxPrice { get; init; }

    /// <summary>
    /// Start date
    /// </summary>
    [Id(3)] public DateTime? MinDate { get; init; }

    /// <summary>
    /// End date
    /// </summary>
    [Id(4)] public DateTime? MaxDate { get; init; }

    /// <summary>
    /// Asset
    /// </summary>
    [Id(5)] public InstrumentState Instrument { get; init; }

    /// <summary>
    /// Criteria
    /// </summary>
    [Id(6)] public Dictionary<string, double> Criteria { get; set; } = new();
  }
}
