using System.Collections.Generic;

namespace Core.Common.States
{
  public record PricesResponse
  {
    /// <summary>
    /// Data
    /// </summary>
    public IList<PriceState> Data { get; init; } = [];

    /// <summary>
    /// Errors
    /// </summary>
    public IList<string> Errors { get; init; } = [];
  }
}
