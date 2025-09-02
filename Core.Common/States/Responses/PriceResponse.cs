using System.Collections.Generic;

namespace Core.Common.States
{
  public record PriceResponse
  {
    /// <summary>
    /// Data
    /// </summary>
    public PriceState Data { get; init; }

    /// <summary>
    /// Errors
    /// </summary>
    public List<string> Errors { get; init; } = [];
  }
}
