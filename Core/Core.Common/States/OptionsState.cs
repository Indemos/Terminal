using Core.Common.Enums;
using Orleans;
using System;
using System.Collections.Generic;

namespace Core.Common.States
{
  [Immutable]
  [GenerateSerializer]
  public record OptionsState
  {
    /// <summary>
    /// List of option contracts for the current point
    /// </summary>
    [Id(0)] public List<InstrumentState> Options { get; init; } = new();
  }
}
