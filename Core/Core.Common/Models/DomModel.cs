using System.Collections.Generic;

namespace Core.Common.Models
{
  public record DomModel
  {
    /// <summary>
    /// Asks
    /// </summary>
    public IList<PriceModel> Asks { get; init; } = [];

    /// <summary>
    /// Bids
    /// </summary>
    public IList<PriceModel> Bids { get; init; } = [];
  }
}
