using System.Collections.Generic;

namespace Core.Models
{
  public record Account
  {
    /// <summary>
    /// Name
    /// </summary>
    public string Descriptor { get; init; }

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
    public Dictionary<string, Instrument> Instruments { get; init; } = new();
  }
}
