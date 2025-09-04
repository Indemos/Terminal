using Core.Common.Implementations;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Common.Indicators
{
  public class PerformanceIndicator : Indicator
  {
    /// <summary>
    /// Calculate indicator value
    /// </summary>
    /// <param name="adapters"></param>
    public async Task<IIndicator> Update(IEnumerable<IGateway> adapters)
    {
      var sum = 0.0 as double?;

      foreach (var adapter in adapters)
      {
        var positions = await adapter.GetPositions();
        var adapterSum = positions.Data.Sum(o => o.Balance.Current);

        sum += adapter.Account.Balance + adapter.Account.Performance + adapterSum;
      }

      Response = Response with { Last = sum };

      return this;
    }
  }
}
