using System.Collections.Generic;
using System.Linq;
using Terminal.Core.ModelSpace;

namespace Terminal.Core.IndicatorSpace
{
  /// <summary>
  /// Implementation
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class PerformanceIndicator : IndicatorModel<IPointModel, PerformanceIndicator>
  {
    /// <summary>
    /// Calculate indicator value
    /// </summary>
    /// <param name="accounts"></param>
    /// <returns></returns>
    public PerformanceIndicator Calculate(IList<IAccountModel> accounts)
    {
      Last = accounts.Sum(o => o.Balance + o.ActivePositions.Sum(v => v.Value.GainLossAverageEstimate));

      return this;
    }
  }
}
