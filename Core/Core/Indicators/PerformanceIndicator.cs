using Core.Conventions;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Indicators
{
  public class PerformanceIndicator
  {
    /// <summary>
    /// Calculate indicator value
    /// </summary>
    /// <param name="adapters"></param>
    public async Task<double?> Update(IEnumerable<IGateway> adapters)
    {
      double? sum = 0.0;

      foreach (var adapter in adapters)
      {
        var account = adapter.Account;
        var positions = await adapter.GetPositions(default);
        var positionsSum = positions.Data.Sum(o => o.Balance.Current);

        sum += account.Balance + account.Performance + positionsSum;
      }

      return sum;
    }
  }
}
