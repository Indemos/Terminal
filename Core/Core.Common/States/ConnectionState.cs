using Orleans;
using System.Collections.Generic;

namespace Core.Common.States
{
  [Immutable]
  [GenerateSerializer]
  public record ConnectionState
  {
    /// <summary>
    /// Instruments
    /// </summary>
    [Id(0)] public Dictionary<string, InstrumentState> Instruments { get; init; } = new();
  }
}
