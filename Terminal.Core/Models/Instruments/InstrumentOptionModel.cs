using System;
using Terminal.Core.EnumSpace;

namespace Terminal.Core.ModelSpace
{
  /// <summary>
  /// Definition
  /// </summary>
  public interface IInstrumentOptionModel : IInstrumentModel
  {
    /// <summary>
    /// Contract size
    /// </summary>
    double? Leverage { get; set; }

    /// <summary>
    /// Open interest
    /// </summary>
    double? OpenInterest { get; set; }

    /// <summary>
    /// Strike price
    /// </summary>
    double? Strike { get; set; }

    /// <summary>
    /// The name of the underlying instrument
    /// </summary>
    string Symbol { get; set; }

    /// <summary>
    /// CALL or PUT
    /// </summary>
    OptionSideEnum? Side { get; set; }

    /// <summary>
    /// Expiration date
    /// </summary>
    DateTime? ExpirationDate { get; set; }

    /// <summary>
    /// Reference to the complex data point
    /// </summary>
    IPointBarModel Bar { get; set; }
  }

  /// <summary>
  /// Implementation
  /// </summary>
  public class InstrumentOptionModel : InstrumentModel, IInstrumentOptionModel
  {
    /// <summary>
    /// Contract size
    /// </summary>
    public virtual double? Leverage { get; set; }

    /// <summary>
    /// Open interest
    /// </summary>
    public virtual double? OpenInterest { get; set; }

    /// <summary>
    /// Strike price
    /// </summary>
    public virtual double? Strike { get; set; }

    /// <summary>
    /// The name of the underlying instrument
    /// </summary>
    public virtual string Symbol { get; set; }

    /// <summary>
    /// CALL or PUT
    /// </summary>
    public virtual OptionSideEnum? Side { get; set; }

    /// <summary>
    /// Expiration date
    /// </summary>
    public virtual DateTime? ExpirationDate { get; set; }

    /// <summary>
    /// Reference to the complex data point
    /// </summary>
    public virtual IPointBarModel Bar { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public InstrumentOptionModel()
    {
      Leverage = 100;
      Bar = new PointBarModel();
    }
  }
}
