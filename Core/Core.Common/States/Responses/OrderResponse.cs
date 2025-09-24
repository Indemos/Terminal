using System.Collections.Generic;

namespace Core.Common.States
{
  public record OrderResponse
  {
    /// <summary>
    /// Data
    /// </summary>
    public OrderState Data { get; init; }

    /// <summary>
    /// Transaction
    /// </summary>
    public OrderState Transaction { get; init; }

    /// <summary>
    /// Errors
    /// </summary>
    public IList<string> Errors { get; init; } = [];
  }
}
