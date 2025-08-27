using Orleans;
using System.Collections.Generic;

namespace Core.Common.States
{
  [Immutable]
  [GenerateSerializer]
  public record DomResponse
  {
    /// <summary>
    /// Data
    /// </summary>
    [Id(0)] public DomState Data { get; init; }

    /// <summary>
    /// Errors
    /// </summary>
    [Id(1)] public List<string> Errors { get; init; } = [];
  }
}
