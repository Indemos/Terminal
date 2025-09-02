using System.Collections.Generic;

namespace Core.Common.States
{
  public record OrdersResponse
  {
    /// <summary>
    /// Data
    /// </summary>
    public List<OrderState> Data { get; init; } = [];

    /// <summary>
    /// Errors
    /// </summary>
    public List<string> Errors { get; init; } = [];
  }
}
