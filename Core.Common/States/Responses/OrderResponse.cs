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
    /// Errors
    /// </summary>
    public List<string> Errors { get; init; } = [];
  }
}
