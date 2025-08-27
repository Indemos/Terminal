using Orleans;
using System.Collections.Generic;

namespace Core.Common.States
{
  [Immutable]
  [GenerateSerializer]
  public record AccountState
  {
    /// <summary>
    /// Balance
    /// </summary>
    [Id(0)] public double? Balance { get; init; } = 0;

    /// <summary>
    /// State of the account in the beginning
    /// </summary>
    [Id(1)] public double? InitialBalance { get; init; } = 0;

    /// <summary>
    /// Name
    /// </summary>
    [Id(2)] public string Descriptor { get; init; }

    /// <summary>
    /// Instruments
    /// </summary>
    [Id(3)] public Dictionary<string, InstrumentState> Instruments { get; init; } = new();
  }
}
