using Orleans;
using System.Collections.Generic;

namespace Core.Common.States
{
  [Immutable]
  [GenerateSerializer]
  public record OrderGroupsResponse
  {
    /// <summary>
    /// Data
    /// </summary>
    [Id(0)] public List<OrderResponse> Data { get; init; } = [];
  }
}
