using System;

namespace Terminal.Core.Models
{
  public class GreekModel : ICloneable
  {
    /// <summary>
    /// Delta
    /// </summary>
    public double? Delta { get; set; }

    /// <summary>
    /// Gamma
    /// </summary>
    public double? Gamma { get; set; }

    /// <summary>
    /// Rho
    /// </summary>
    public double? Rho { get; set; }

    /// <summary>
    /// Theta
    /// </summary>
    public double? Theta { get; set; }

    /// <summary>
    /// Vega
    /// </summary>
    public double? Vega { get; set; }

    /// <summary>
    /// Clone
    /// </summary>
    public object Clone() => MemberwiseClone() as GreekModel;
  }
}
