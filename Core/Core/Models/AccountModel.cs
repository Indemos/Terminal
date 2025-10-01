using System.Collections.Generic;

namespace Core.Models
{
  public record AccountModel
  {
    /// <summary>
    /// Name
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Balance
    /// </summary>
    public double? Balance { get; init; } = 0;

    /// <summary>
    /// State of the account in the beginning
    /// </summary>
    public double? Performance { get; init; } = 0;

    /// <summary>
    /// Instruments
    /// </summary>
    public Dictionary<string, InstrumentModel> Instruments { get; init; } = new();
  }
}
