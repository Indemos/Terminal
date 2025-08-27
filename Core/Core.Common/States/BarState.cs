using Orleans;

namespace Core.Common.States
{
  [Immutable]
  [GenerateSerializer]
  public record BarState
  {
    /// <summary>
    /// Lowest price of the bar
    /// </summary>
    [Id(0)] public double? Low { get; init; }

    /// <summary>
    /// Highest price of the bar
    /// </summary>
    [Id(1)] public double? High { get; init; }

    /// <summary>
    /// Open price of the bar
    /// </summary>
    [Id(2)] public double? Open { get; init; }

    /// <summary>
    /// Close price of the bar
    /// </summary>
    [Id(3)] public double? Close { get; init; }
  }
}
