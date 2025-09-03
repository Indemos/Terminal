using System.Collections.Generic;

namespace Core.Common.States
{
  public record OptionsState
  {
    /// <summary>
    /// List of option contracts for the current point
    /// </summary>
    public IList<InstrumentState> Options { get; init; } = [];
  }
}
