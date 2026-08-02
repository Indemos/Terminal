using Core.Enums;
using System;

namespace Core.Models
{
  public record Instrument
  {
    /// <summary>
    /// ID
    /// </summary>
    public string Id { get; init; }

    /// <summary>
    /// Name
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Exchange
    /// </summary>
    public string Exchange { get; init; }

    /// <summary>
    /// Commission
    /// </summary>
    public double? Commission { get; init; } = 0;

    /// <summary>
    /// Contract size for 1 trading lot in currencies and futures
    /// </summary>
    public double? ContractSize { get; init; } = 1;

    /// <summary>
    /// Contract size
    /// </summary>
    public double? Leverage { get; init; } = 1;

    /// <summary>
    /// Minimum price change 
    /// </summary>
    public double? StepSize { get; init; } = 0.01;

    /// <summary>
    /// Price change within one tick
    /// </summary>
    public double? StepValue { get; init; } = 0.01;

    /// <summary>
    /// Aggregation period for the quotes
    /// </summary>
    public TimeSpan? TimeFrame { get; init; }

    /// <summary>
    /// Instrument type
    /// </summary>
    public InstrumentEnum? Type { get; init; }

    /// <summary>
    /// Current price data 
    /// </summary>
    public Price Price { get; init; }

    /// <summary>
    /// Undelying symbol
    /// </summary>
    public Instrument Basis { get; init; }

    /// <summary>
    /// Base currency contract
    /// </summary>
    public Currency Currency { get; init; } = new Currency { Name = nameof(CurrencyEnum.USD) };

    /// <summary>
    /// Options and futures specification
    /// </summary>
    public Derivative Derivative { get; init; }
  }
}
