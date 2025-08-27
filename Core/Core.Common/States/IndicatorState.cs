using Core.Common.States;
using Orleans;

namespace Core.Common.Domains
{
  [Immutable]
  [GenerateSerializer]
  public record IndicatorState
  {
    /// <summary>
    /// Name
    /// </summary>
    [Id(0)] public string Name { get; set; }

    /// <summary>
    /// Point
    /// </summary>
    [Id(1)] public PriceState Point { get; init; } = new();
  }
}
