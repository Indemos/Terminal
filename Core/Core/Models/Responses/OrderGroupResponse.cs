using System.Collections.Generic;

namespace Core.Models
{
  public record OrderGroupResponse
  {
    /// <summary>
    /// Data
    /// </summary>
    public IList<OrderResponse> Data { get; init; } = [];
  }
}
