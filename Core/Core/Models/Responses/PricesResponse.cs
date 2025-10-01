using System.Collections.Generic;

namespace Core.Models
{
  public record PricesResponse
  {
    /// <summary>
    /// Data
    /// </summary>
    public IList<PriceModel> Data { get; init; } = [];

    /// <summary>
    /// Errors
    /// </summary>
    public IList<string> Errors { get; init; } = [];
  }
}
