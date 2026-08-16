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
    public List<Price> Items { get; init; } = [];

    /// <summary>
    /// Ticks aggregated into bars
    /// </summary>
    public List<Price> ItemGroups { get; init; } = [];
  }
}
