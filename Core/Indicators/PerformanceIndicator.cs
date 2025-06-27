using System.Collections.Generic;
using System.Linq;
using Terminal.Core.Domains;

namespace Terminal.Core.Indicators
{
  public class PerformanceIndicator : Indicator<PerformanceIndicator>
  {
    /// <summary>
    /// Calculate indicator value
    /// </summary>
    /// <param name="accounts"></param>
    /// <returns></returns>
    public PerformanceIndicator Update(IList<IAccount> accounts)
    {
      Point.Last = accounts.Sum(o => o.Balance + o.Positions.Sum(v => v.Value.GetValueEstimate() ?? 0));

      return this;
    }
  }
}
