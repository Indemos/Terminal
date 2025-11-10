using System.Collections.Generic;

namespace Core.Models
{
  public record Dom
  {
    /// <summary>
    /// Asks
    /// </summary>
    public IList<Price> Asks { get; init; } = [];

    /// <summary>
    /// Bids
    /// </summary>
    public IList<Price> Bids { get; init; } = [];
  }
}
