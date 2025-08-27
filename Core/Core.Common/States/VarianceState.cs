using Orleans;

namespace Core.Common.States
{
  [Immutable]
  [GenerateSerializer]
  public record VarianceState
  {
    /// <summary>
    /// Delta
    /// </summary>
    [Id(0)] public double? Delta { get; init; }

    /// <summary>
    /// Gamma
    /// </summary>
    [Id(1)] public double? Gamma { get; init; }

    /// <summary>
    /// Rho
    /// </summary>
    [Id(2)] public double? Rho { get; init; }

    /// <summary>
    /// Theta
    /// </summary>
    [Id(3)] public double? Theta { get; init; }

    /// <summary>
    /// Vega
    /// </summary>
    [Id(4)] public double? Vega { get; init; }
  }
}
