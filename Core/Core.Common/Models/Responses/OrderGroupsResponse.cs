using System.Collections.Generic;

namespace Core.Common.Models
{
  public record OrderGroupsResponse
  {
    /// <summary>
    /// Data
    /// </summary>
    public IList<OrderResponse> Data { get; init; } = [];
  }
}
