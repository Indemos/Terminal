using System.Collections.Generic;

namespace Core.Common.States
{
  public record InstrumentsResponse
  {
    /// <summary>
    /// Data
    /// </summary>
    public IList<InstrumentState> Data { get; init; } = [];

    /// <summary>
    /// Errors
    /// </summary>
    public IList<string> Errors { get; init; } = [];
  }
}
