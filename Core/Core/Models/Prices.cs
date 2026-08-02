using System.Collections.Generic;

namespace Core.Models
{
  public record Prices
  {
    /// <summary>
    /// Instrument
    /// </summary>
    public Instrument Instrument { get; init; }

    /// <summary>
    /// Ticks
    /// </summary>
    public IList<Price> Items { get; init; } = [];

    /// <summary>
    /// Ticks aggregated into bars
    /// </summary>
    public IList<Price> ItemGroups { get; init; } = [];
  }
}
