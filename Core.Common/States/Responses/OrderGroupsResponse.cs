using System.Collections.Generic;

namespace Core.Common.States
{
  public record OrderGroupsResponse
  {
    /// <summary>
    /// Data
    /// </summary>
    public List<OrderResponse> Data { get; init; } = [];
  }
}
