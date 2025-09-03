using System.Collections.Generic;

namespace Core.Common.States
{
  public record ConnectionState
  {
    /// <summary>
    /// Instruments
    /// </summary>
    public Dictionary<string, InstrumentState> Instruments { get; init; } = [];
  }
}
