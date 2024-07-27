using System;
using Terminal.Core.Enums;

namespace Terminal.Core.Models
{
  public class DerivativeModel : ICloneable
  {
    /// <summary>
    /// Strike price
    /// </summary>
    public double? Strike { get; set; }

    /// <summary>
    /// Open interest
    /// </summary>
    public double? OpenInterest { get; set; }

    /// <summary>
    /// Intrinsic value
    /// </summary>
    public double? IntrinsicValue { get; set; }

    /// <summary>
    /// Volume
    /// </summary>
    public double? Volume { get; set; }

    /// <summary>
    /// Volatility
    /// </summary>
    public double? Volatility { get; set; }

    /// <summary>
    /// CALL or PUT
    /// </summary>
    public OptionSideEnum? Side { get; set; }

    /// <summary>
    /// Expiration date
    /// </summary>
    public DateTime? Expiration { get; set; }

    /// <summary>
    /// Option metrics
    /// </summary>
    public VariableModel Variables { get; set; }

    /// <summary>
    /// Option price data
    /// </summary>
    public PointModel Contract { get; set; }

    /// <summary>
    /// Current price data 
    /// </summary>
    public PointModel Basis { get; set; }

    /// <summary>
    /// Clone
    /// </summary>
    public object Clone() => MemberwiseClone() as DerivativeModel;
  }
}
