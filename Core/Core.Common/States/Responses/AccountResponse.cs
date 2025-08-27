using Orleans;
using System.Collections.Generic;

namespace Core.Common.States
{
  [Immutable]
  [GenerateSerializer]
  public record AccountResponse
  {
    /// <summary>
    /// Data
    /// </summary>
    [Id(0)] public AccountState Data { get; init; }

    /// <summary>
    /// Errors
    /// </summary>
    [Id(1)] public List<string> Errors { get; init; } = [];
  }
}
