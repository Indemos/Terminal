using Orleans;
using System.Collections.Generic;

namespace Core.Common.States
{
  [Immutable]
  [GenerateSerializer]
  public record DomState
  {
    [Id(0)] public List<PriceState> Asks { get; init; } = new();
    [Id(1)] public List<PriceState> Bids { get; init; } = new();
  }
}
