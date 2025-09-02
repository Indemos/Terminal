using System.Collections.Generic;

namespace Core.Common.States
{
  public record DomState
  {
    /// <summary>
    /// Asks
    /// </summary>
    public List<PriceState> Asks { get; init; } = [];

    /// <summary>
    /// Bids
    /// </summary>
    public List<PriceState> Bids { get; init; } = [];
  }
}
