using System.Collections.Generic;

namespace Core.Models
{
  public record PriceResponse
  {
    /// <summary>
    /// Data
    /// </summary>
    public Price Data { get; init; }

    /// <summary>
    /// Errors
    /// </summary>
    public IList<string> Errors { get; init; } = [];
  }
}
