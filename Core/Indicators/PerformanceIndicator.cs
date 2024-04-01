using System.Collections.Generic;
using System.Linq;
using Terminal.Core.Domains;
using Terminal.Core.Models;

namespace Terminal.Core.Indicators
{
  /// <summary>
  /// Implementation
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class PerformanceIndicator : Indicator<PerformanceIndicator>
  {
    /// <summary>
    /// Calculate indicator value
    /// </summary>
    /// <param name="accounts"></param>
    /// <returns></returns>
    public PerformanceIndicator Calculate(IList<IAccount> accounts)
    {
      var point = Point ?? new PointModel();

      point.Price = accounts.Sum(o => o.Balance + o.ActivePositions.Sum(v => v.Value?.GainLossAverageEstimate));
      Point = point;

      return this;
    }
  }
}
