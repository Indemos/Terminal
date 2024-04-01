using System;

namespace Terminal.Core.Models
{
  public struct DerivativeModel
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
  }
}
