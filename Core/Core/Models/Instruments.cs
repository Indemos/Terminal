using System.Collections.Generic;

namespace Core.Models
{
  public record Instruments
  {
    /// <summary>
    /// Instruments
    /// </summary>
    public List<Instrument> Items { get; init; } = [];
  }
}
