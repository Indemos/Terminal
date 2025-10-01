using System.Collections.Generic;

namespace Core.Common.Models
{
  public record OrderResponse
  {
    /// <summary>
    /// Data
    /// </summary>
    public OrderModel Data { get; init; }

    /// <summary>
    /// Transaction
    /// </summary>
    public OrderModel Transaction { get; init; }

    /// <summary>
    /// Errors
    /// </summary>
    public IList<string> Errors { get; init; } = [];
  }
}
