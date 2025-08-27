using Core.Common.Grains;
using Orleans;
using System.Collections.Generic;

namespace Core.Common.States
{
  [Immutable]
  [GenerateSerializer]
  public record PositionsState
  {
    /// <summary>
    /// Active orders
    /// </summary>
    [Id(0)] public Dictionary<string, PositionGrain> Grains { get; init; } = new();
  }
}
