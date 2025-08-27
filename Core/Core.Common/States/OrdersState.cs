using Core.Common.Grains;
using Orleans;
using System.Collections.Generic;

namespace Core.Common.States
{
  [Immutable]
  [GenerateSerializer]
  public record OrdersState
  {
    /// <summary>
    /// Active orders
    /// </summary>
    [Id(0)] public Dictionary<string, OrderGrain> Grains { get; init; } = new();
  }
}
