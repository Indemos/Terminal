using System.Collections.Generic;

namespace Core.Models
{
  public record OrderResponse
  {
    /// <summary>
    /// Data
    /// </summary>
    public Order Data { get; init; }

    /// <summary>
    /// Transaction
    /// </summary>
    public Order Transaction { get; init; }

    /// <summary>
    /// Errors
    /// </summary>
    public IList<string> Errors { get; init; } = [];
  }
}
