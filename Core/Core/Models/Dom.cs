using System.Collections.Generic;

namespace Core.Models
{
  public record Dom
  {
    /// <summary>
    /// Asks
    /// </summary>
    public List<Price> Asks { get; init; } = [];

    /// <summary>
    /// Bids
    /// </summary>
    public List<Price> Bids { get; init; } = [];
  }
}
