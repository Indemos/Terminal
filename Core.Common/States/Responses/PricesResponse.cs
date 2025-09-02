using System.Collections.Generic;

namespace Core.Common.States
{
  public record PricesResponse
  {
    /// <summary>
    /// Data
    /// </summary>
    public List<PriceState> Data { get; init; } = [];

    /// <summary>
    /// Errors
    /// </summary>
    public List<string> Errors { get; init; } = [];
  }
}
