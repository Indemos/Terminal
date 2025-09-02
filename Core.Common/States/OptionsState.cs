using Orleans;
using System.Collections.Generic;

namespace Core.Common.States
{
  public record OptionsState
  {
    /// <summary>
    /// List of option contracts for the current point
    /// </summary>
    public List<InstrumentState> Options { get; init; } = new();
  }
}
