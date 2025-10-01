using System.Collections.Generic;

namespace Core.Models
{
  public record OrdersResponse
  {
    /// <summary>
    /// Data
    /// </summary>
    public IList<OrderModel> Data { get; init; } = [];

    /// <summary>
    /// Errors
    /// </summary>
    public IList<string> Errors { get; init; } = [];
  }
}
