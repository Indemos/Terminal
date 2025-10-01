using System.Collections.Generic;

namespace Core.Models
{
  public record OrderGroupsResponse
  {
    /// <summary>
    /// Data
    /// </summary>
    public IList<OrderResponse> Data { get; init; } = [];
  }
}
