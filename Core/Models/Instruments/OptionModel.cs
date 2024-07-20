using System;
using Terminal.Core.Domains;
using Terminal.Core.Enums;

namespace Terminal.Core.Models
{
  public class OptionModel : ICloneable
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
    public DateTime? ExpirationDate { get; set; }

    /// <summary>
    /// Option contract data
    /// </summary>
    public PointModel Option { get; set; }

    /// <summary>
    /// Symbol data
    /// </summary>
    public PointModel Point { get; set; }

    /// <summary>
    /// Risk and derivatives
    /// </summary>
    public GreekModel Greeks { get; set; }

    /// <summary>
    /// Clone
    /// </summary>
    public object Clone() => MemberwiseClone() as OptionModel;
  }
}
