using System;
using Terminal.Core.Domains;
using Terminal.Core.Enums;

namespace Terminal.Core.Models
{
  public struct OptionModel
  {
    /// <summary>
    /// Name
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// The name of the underlying instrument
    /// </summary>
    public string BaseName { get; set; }

    /// <summary>
    /// Strike price
    /// </summary>
    public double? Strike { get; set; }

    /// <summary>
    /// Contract size
    /// </summary>
    public double? Leverage { get; set; }

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
    /// Instrument
    /// </summary>
    public IInstrument Instrument { get; set; }

    /// <summary>
    /// Point
    /// </summary>
    public PointModel? Point { get; set; }

    /// <summary>
    /// Risk and derivatives
    /// </summary>
    public DerivativeModel Derivatives { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public OptionModel()
    {
      Leverage = 100;
    }
  }
}
