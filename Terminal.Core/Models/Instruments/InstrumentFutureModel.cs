using System;

namespace Terminal.Core.ModelSpace
{
  /// <summary>
  /// Definition
  /// </summary>
  public interface IInstrumentFutureModel : IInstrumentModel
  {
    /// <summary>
    /// Expiration date
    /// </summary>
    DateTime? ExpirationDate { get; set; }
  }

  /// <summary>
  /// Implementation
  /// </summary>
  public class InstrumentFutureModel : InstrumentModel, IInstrumentFutureModel
  {
    /// <summary>
    /// Expiration date
    /// </summary>
    public virtual DateTime? ExpirationDate { get; set; }
  }
}
