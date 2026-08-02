using System.Collections.Generic;

namespace Core.Models
{
  public record InstrumentsResponse
  {
    /// <summary>
    /// Data
    /// </summary>
    public IList<Instrument> Data { get; init; } = [];

    /// <summary>
    /// Errors
    /// </summary>
    public IList<string> Errors { get; init; } = [];
  }
}
