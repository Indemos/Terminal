using Core.Common.Enums;
using System;

namespace Core.Common.States
{
  public record DerivativeState
  {
    /// <summary>
    /// Strike price
    /// </summary>
    public double? Strike { get; init; }

    /// <summary>
    /// Open interest
    /// </summary>
    public double? OpenInterest { get; init; }

    /// <summary>
    /// Intrinsic value
    /// </summary>
    public double? IntrinsicValue { get; init; }

    /// <summary>
    /// Implied volatility
    /// </summary>
    public double? Volatility { get; init; }

    /// <summary>
    /// CALL or PUT
    /// </summary>
    public OptionSideEnum? Side { get; init; }

    /// <summary>
    /// Expiration type
    /// </summary>
    public ExpirationTypeEnum? ExpirationType { get; init; }

    /// <summary>
    /// Expiration date
    /// </summary>
    public DateTime? ExpirationDate { get; init; }

    /// <summary>
    /// Last trading date
    /// </summary>
    public DateTime? TradeDate { get; init; }

    /// <summary>
    /// Option metrics
    /// </summary>
    public VarianceState Variance { get; init; }
  }
}
