using System.Collections.Generic;

namespace Core.Common.States
{
  public record InstrumentsResponse
  {
    /// <summary>
    /// Data
    /// </summary>
    public List<InstrumentState> Data { get; init; } = [];

    /// <summary>
    /// Errors
    /// </summary>
    public List<string> Errors { get; init; } = [];
  }
}
