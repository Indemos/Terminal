using Core.Grains;
using System.Collections.Generic;

namespace Core.Models
{
  public record TransactionsModel
  {
    /// <summary>
    /// History of completed deals, closed positions
    /// </summary>
    public IList<ITransactionGrain> Grains { get; init; } = [];
  }
}
