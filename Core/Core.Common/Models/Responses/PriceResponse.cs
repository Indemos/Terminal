using System.Collections.Generic;

namespace Core.Common.Models
{
  public record PriceResponse
  {
    /// <summary>
    /// Data
    /// </summary>
    public PriceModel Data { get; init; }

    /// <summary>
    /// Errors
    /// </summary>
    public IList<string> Errors { get; init; } = [];
  }
}
