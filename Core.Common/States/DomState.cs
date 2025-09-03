using System.Collections.Generic;

namespace Core.Common.States
{
  public record DomState
  {
    /// <summary>
    /// Asks
    /// </summary>
    public IList<PriceState> Asks { get; init; } = [];

    /// <summary>
    /// Bids
    /// </summary>
    public IList<PriceState> Bids { get; init; } = [];
  }
}
