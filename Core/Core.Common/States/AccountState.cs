using System.Collections.Generic;

namespace Core.Common.States
{
  public record AccountState
  {
    /// <summary>
    /// Balance
    /// </summary>
    public double? Balance { get; init; } = 0;

    /// <summary>
    /// State of the account in the beginning
    /// </summary>
    public double? Performance { get; init; } = 0;

    /// <summary>
    /// Name
    /// </summary>
    public string Descriptor { get; init; }

    /// <summary>
    /// Instruments
    /// </summary>
    public Dictionary<string, InstrumentState> Instruments { get; init; } = new();
  }
}
