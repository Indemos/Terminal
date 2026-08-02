using System.Collections.Generic;

namespace Core.Models
{
  public record Instruments
  {
    /// <summary>
    /// Instruments
    /// </summary>
    public IList<Instrument> Items { get; init; } = [];
  }
}
