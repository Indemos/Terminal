using Terminal.Core.CollectionSpace;
using Terminal.Core.ExtensionSpace;
using Terminal.Core.ModelSpace;
using Terminal.Core.ServiceSpace;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Terminal.Core.IndicatorSpace
{
  /// <summary>
  /// Implementation
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class RelativeStrengthIndicator : IndicatorModel<IPointModel, RelativeStrengthIndicator>
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
    public override RelativeStrengthIndicator Calculate(IIndexCollection<IPointModel> collection)
    {
      var currentPoint = collection.ElementAtOrDefault(collection.Count - 1);

      if (currentPoint == null)
      {
        return this;
      }

      var positives = new List<double>(Interval);
      var negatives = new List<double>(Interval);
      var compService = InstanceService<ComputationService>.Instance;

      for (var i = 1; i <= Interval; i++)
      {
        var nextPrice = collection.ElementAtOrDefault(collection.Count - i);
        var previousPrice = collection.ElementAtOrDefault(collection.Count - i - 1);

        if (nextPrice != null && previousPrice != null)
        {
          positives.Add(Math.Max(nextPrice.Bar.Close.Value - previousPrice.Bar.Close.Value, 0.0));
          negatives.Add(Math.Max(previousPrice.Bar.Close.Value - nextPrice.Bar.Close.Value, 0.0));
        }
      }

      var averagePositive = compService.SimpleAverage(positives, positives.Count - 1, Interval);
      var averageNegative = compService.SimpleAverage(negatives, negatives.Count - 1, Interval);
      var average = averageNegative.IsEqual(0) ? 1.0 : averagePositive / averageNegative;
      var nextValue = 100.0 - 100.0 / (1.0 + average);
      var nextIndicatorPoint = new PointModel
      {
        Time = currentPoint.Time,
        TimeFrame = currentPoint.TimeFrame,
        Last = nextValue,
        Bar = new PointBarModel
        {
          Close = nextValue
        }
      };

      var previousIndicatorPoint = Values.ElementAtOrDefault(collection.Count - 1);

      if (previousIndicatorPoint == null)
      {
        Values.Add(nextIndicatorPoint);
      }

      Values[collection.Count - 1] = nextIndicatorPoint;

      currentPoint.Series[Name] = currentPoint.Series.TryGetValue(Name, out IPointModel seriesItem) ? seriesItem : new RelativeStrengthIndicator();
      currentPoint.Series[Name].Bar.Close = currentPoint.Series[Name].Last = nextIndicatorPoint.Bar.Close;
      currentPoint.Series[Name].Time = currentPoint.Time;
      currentPoint.Series[Name].View = View;

      Last = Bar.Close = currentPoint.Series[Name].Bar.Close;

      return this;
    }
  }
}
