using System;
using Terminal.Core.Domains;

namespace Terminal.Core.Models
{
  public struct FutureModel
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
    public IInstrument Instrument { get; set; }
  }
}
