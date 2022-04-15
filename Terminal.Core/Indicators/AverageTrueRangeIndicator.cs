using Terminal.Core.CollectionSpace;
using Terminal.Core.ModelSpace;
using System;
using System.Linq;

namespace Terminal.Core.IndicatorSpace
{
  /// <summary>
  /// Implementation
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class AverageTrueRangeIndicator : IndicatorModel<IPointModel, AverageTrueRangeIndicator>
  {
    /// <summary>
    /// Number of bars to average
    /// </summary>
    public int Interval { get; set; }

    /// <summary>
    /// Preserve last calculated value
    /// </summary>
    public IIndexCollection<IPointModel> Values { get; private set; } = new IndexCollection<IPointModel>();

    /// <summary>
    /// Calculate single value
    /// </summary>
    /// <param name="collection"></param>
    /// <returns></returns>
    public override AverageTrueRangeIndicator Calculate(IIndexCollection<IPointModel> collection)
    {
      var currentPoint = collection.ElementAtOrDefault(collection.Count - 1);
      var previousPoint = collection.ElementAtOrDefault(collection.Count - 2);

      if (currentPoint == null || previousPoint == null)
      {
        return this;
      }

      var variance =
        Math.Max(currentPoint.Bar.High.Value, previousPoint.Bar.Close.Value) -
        Math.Min(currentPoint.Bar.Low.Value, previousPoint.Bar.Close.Value);

      var nextIndicatorPoint = new PointModel
      {
        Time = currentPoint.Time,
        TimeFrame = currentPoint.TimeFrame,
        Last = variance,
        Bar = new PointBarModel
        {
          Close = variance
        }
      };

      if (Values.Count > Interval)
      {
        nextIndicatorPoint.Bar.Close = nextIndicatorPoint.Last = (Values.ElementAtOrDefault(Values.Count - 1).Bar.Close * Math.Max(Interval - 1, 0) + variance) / Interval;
      }

      var previousIndicatorPoint = Values.ElementAtOrDefault(collection.Count - 1);

      if (previousIndicatorPoint == null)
      {
        Values.Add(nextIndicatorPoint);
      }

      Values[collection.Count - 1] = nextIndicatorPoint;

      currentPoint.Series[Name] = currentPoint.Series.TryGetValue(Name, out IPointModel o) ? o : new AverageTrueRangeIndicator();
      currentPoint.Series[Name].Bar.Close = currentPoint.Series[Name].Last = nextIndicatorPoint.Bar.Close;
      currentPoint.Series[Name].Time = currentPoint.Time;
      currentPoint.Series[Name].View = View;

      Last = Bar.Close = currentPoint.Series[Name].Bar.Close;

      return this;
    }
  }
}
