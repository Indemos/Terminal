using Core.Common.Grains;
using Orleans;
using System.Collections.Generic;

namespace Core.Common.States
{
  [Immutable]
  [GenerateSerializer]
  public record TransactionsState
  {
    /// <summary>
    /// History of completed deals, closed positions
    /// </summary>
    [Id(0)] public List<TransactionGrain> Grains { get; init; } = new();
  }
}
