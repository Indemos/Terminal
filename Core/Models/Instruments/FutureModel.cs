using System;
using Terminal.Core.Domains;

namespace Terminal.Core.Models
{
  public class FutureModel : ICloneable
  {
    /// <summary>
    /// Name
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Expiration date
    /// </summary>
    public DateTime? ExpirationDate { get; set; }

    /// <summary>
    /// Instrument
    /// </summary>
    public InstrumentModel Instrument { get; set; }

    /// <summary>
    /// Clone
    /// </summary>
    public object Clone() => MemberwiseClone() as FutureModel;
  }
}
