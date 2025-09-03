using System.Collections.Generic;

namespace Core.Common.States
{
  public record OrdersResponse
  {
    /// <summary>
    /// Data
    /// </summary>
    public IList<OrderState> Data { get; init; } = [];

    /// <summary>
    /// Errors
    /// </summary>
    public IList<string> Errors { get; init; } = [];
  }
}
