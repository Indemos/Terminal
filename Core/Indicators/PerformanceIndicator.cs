using Core.CollectionSpace;
using Core.ModelSpace;
using System.Collections.Generic;
using System.Linq;

namespace Core.IndicatorSpace
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
    /// <param name="currentPoint"></param>
    /// <returns></returns>
    public PerformanceIndicator Calculate(ITimeCollection<IPointModel> collection, IEnumerable<IAccountModel> accounts)
    {
      var currentPoint = collection.ElementAtOrDefault(collection.Count - 1);

      if (currentPoint == null)
      {
        return this;
      }

      currentPoint.Series[Name] = currentPoint.Series.TryGetValue(Name, out IPointModel seriesItem) ? seriesItem : new PerformanceIndicator();
      currentPoint.Series[Name].Time = currentPoint.Time;
      currentPoint.Series[Name].TimeFrame = currentPoint.TimeFrame;
      currentPoint.Series[Name].Chart = Chart;
      currentPoint.Series[Name].Bar.Close = currentPoint.Series[Name].Last = accounts.Sum(o => o.Balance + o.ActivePositions.Sum(v => v.GainLossAverageEstimate));

      Last = Bar.Close = currentPoint.Series[Name].Bar.Close;

      return this;
    }
  }
}
