using Core.Common.Enums;
using Orleans;
using System;

namespace Core.Common.States
{
  [Immutable]
  [GenerateSerializer]
  public record InstrumentState
  {
    /// <summary>
    /// ID
    /// </summary>
    [Id(0)] public string Id { get; init; }

    /// <summary>
    /// Name
    /// </summary>
    [Id(1)] public string Name { get; init; }

    /// <summary>
    /// Exchange
    /// </summary>
    [Id(2)] public string Exchange { get; init; }

    /// <summary>
    /// Commission
    /// </summary>
    [Id(3)] public double? Commission { get; init; } = 0;

    /// <summary>
    /// Contract size for 1 trading lot in currencies and futures
    /// </summary>
    [Id(4)] public double? ContractSize { get; init; } = 1;

    /// <summary>
    /// Contract size
    /// </summary>
    [Id(5)] public double? Leverage { get; init; } = 1;

    /// <summary>
    /// Tick size, i.e. minimum price change 
    /// </summary>
    [Id(6)] public double? StepSize { get; init; } = 0.01;

    /// <summary>
    /// Tick value, i.e. how much price change within one tick
    /// </summary>
    [Id(7)] public double? StepValue { get; init; } = 0.01;

    /// <summary>
    /// Aggregation period for the quotes
    /// </summary>
    [Id(8)] public TimeSpan? TimeFrame { get; init; }

    /// <summary>
    /// Instrument type
    /// </summary>
    [Id(9)] public InstrumentEnum? Type { get; init; }

    /// <summary>
    /// Current price data 
    /// </summary>
    [Id(10)] public PriceState Price { get; init; }

    /// <summary>
    /// Undelying symbol
    /// </summary>
    [Id(11)] public InstrumentState Basis { get; init; }

    /// <summary>
    /// Base currency contract
    /// </summary>
    [Id(12)] public CurrencyState Currency { get; init; }

    /// <summary>
    /// Options and futures specification
    /// </summary>
    [Id(13)] public DerivativeState Derivative { get; init; }
  }
}
