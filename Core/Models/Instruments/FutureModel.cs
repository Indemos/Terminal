using System;
using Terminal.Core.Domains;

namespace Terminal.Core.Models
{
    public class FutureModel : ICloneable
  {
    /// <summary>
    /// Name
    /// </summary>
    public virtual string Name { get; set; }

    /// <summary>
    /// Expiration date
    /// </summary>
    public virtual DateTime? ExpirationDate { get; set; }

    /// <summary>
    /// Instrument
    /// </summary>
    public virtual IInstrument Instrument { get; set; }

    /// <summary>
    /// Clone
    /// </summary>
    public virtual object Clone() => MemberwiseClone() as FutureModel;
  }
}
