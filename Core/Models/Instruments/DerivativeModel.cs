using System;

namespace Terminal.Core.Models
{
  public class DerivativeModel : ICloneable
  {
    /// <summary>
    /// Delta
    /// </summary>
    public virtual decimal? Delta { get; set; }

    /// <summary>
    /// Gamma
    /// </summary>
    public virtual decimal? Gamma { get; set; }

    /// <summary>
    /// Rho
    /// </summary>
    public virtual decimal? Rho { get; set; }

    /// <summary>
    /// Theta
    /// </summary>
    public virtual decimal? Theta { get; set; }

    /// <summary>
    /// Vega
    /// </summary>
    public virtual decimal? Vega { get; set; }

    /// <summary>
    /// Clone
    /// </summary>
    public virtual object Clone() => MemberwiseClone() as DerivativeModel;
  }
}
