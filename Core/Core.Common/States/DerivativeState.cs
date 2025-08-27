using Core.Common.Enums;
using Orleans;
using System;

namespace Core.Common.States
{
  [Immutable]
  [GenerateSerializer]
  public record DerivativeState
  {
    /// <summary>
    /// Strike price
    /// </summary>
    [Id(0)] public double? Strike { get; init; }

    /// <summary>
    /// Open interest
    /// </summary>
    [Id(1)] public double? OpenInterest { get; init; }

    /// <summary>
    /// Intrinsic value
    /// </summary>
    [Id(2)] public double? IntrinsicValue { get; init; }

    /// <summary>
    /// Implied volatility
    /// </summary>
    [Id(3)] public double? Volatility { get; init; }

    /// <summary>
    /// CALL or PUT
    /// </summary>
    [Id(4)] public OptionSideEnum? Side { get; init; }

    /// <summary>
    /// Expiration type
    /// </summary>
    [Id(5)] public ExpirationTypeEnum? ExpirationType { get; init; }

    /// <summary>
    /// Expiration date
    /// </summary>
    [Id(6)] public DateTime? ExpirationDate { get; init; }

    /// <summary>
    /// Last trading date
    /// </summary>
    [Id(7)] public DateTime? TradeDate { get; init; }

    /// <summary>
    /// Option metrics
    /// </summary>
    [Id(8)] public VarianceState Variance { get; init; }
  }
}
