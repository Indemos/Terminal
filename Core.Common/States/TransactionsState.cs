using Core.Common.Grains;
using System.Collections.Generic;

namespace Core.Common.States
{
  public record TransactionsState
  {
    /// <summary>
    /// History of completed deals, closed positions
    /// </summary>
    public List<ITransactionGrain> Grains { get; init; } = [];
  }
}
