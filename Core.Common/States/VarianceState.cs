using Orleans;

namespace Core.Common.States
{
  public record VarianceState
  {
    /// <summary>
    /// Delta
    /// </summary>
    public double? Delta { get; init; }

    /// <summary>
    /// Gamma
    /// </summary>
    public double? Gamma { get; init; }

    /// <summary>
    /// Rho
    /// </summary>
    public double? Rho { get; init; }

    /// <summary>
    /// Theta
    /// </summary>
    public double? Theta { get; init; }

    /// <summary>
    /// Vega
    /// </summary>
    public double? Vega { get; init; }
  }
}
