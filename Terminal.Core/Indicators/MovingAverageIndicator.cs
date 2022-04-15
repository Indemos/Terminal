using Terminal.Core.CollectionSpace;
using Terminal.Core.ModelSpace;
using Terminal.Core.ExtensionSpace;
using Terminal.Core.ServiceSpace;
using System.Linq;

namespace Terminal.Core.IndicatorSpace
{
  /// <summary>
  /// Calculation mode
  /// </summary>
  public enum MovingAverageEnum : byte
  {
    Bid = 1,
    Ask = 2,
    Close = 3
  }

  /// <summary>
  /// Implementation
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class MovingAverageIndicator : IndicatorModel<IPointModel, MovingAverageIndicator>
  {
    /// <summary>
    /// Number of bars to average
    /// </summary>
    public int Interval { get; set; }

    /// <summary>
    /// Calculation mode
    /// </summary>
    public MovingAverageEnum Mode { get; set; }

    /// <summary>
    /// Preserve last calculated value
    /// </summary>
    public IIndexCollection<IPointModel> Values { get; private set; } = new IndexCollection<IPointModel>();

    /// <summary>
    /// Calculate single value
    /// </summary>
    /// <param name="collection"></param>
    /// <returns></returns>
    public override MovingAverageIndicator Calculate(IIndexCollection<IPointModel> collection)
    {
      var currentPoint = collection.ElementAtOrDefault(collection.Count - 1);

      if (currentPoint == null)
      {
        return this;
      }

      var pointPrice = currentPoint.Bar.Close;
      var compService = InstanceService<ComputationService>.Instance;

      switch (Mode)
      {
        case MovingAverageEnum.Bid: pointPrice = currentPoint.Bid; break;
        case MovingAverageEnum.Ask: pointPrice = currentPoint.Ask; break;
      }

      var nextIndicatorPoint = new PointModel
      {
        Last = pointPrice,
        Time = currentPoint.Time,
        TimeFrame = currentPoint.TimeFrame,
        Bar = new PointBarModel
        {
          Close = pointPrice
        }
      };

      var previousIndicatorPoint = Values.ElementAtOrDefault(collection.Count - 1);

      if (previousIndicatorPoint == null)
      {
        Values.Add(nextIndicatorPoint);
      }

      Values[collection.Count - 1] = nextIndicatorPoint;

      var average = compService.LinearWeightAverage(Values.Select(o => o.Bar.Close.Value), Values.Count - 1, Interval);

      currentPoint.Series[Name] = currentPoint.Series.TryGetValue(Name, out IPointModel seriesItem) ? seriesItem : new MovingAverageIndicator();
      currentPoint.Series[Name].Bar.Close = currentPoint.Series[Name].Last = average.IsEqual(0) ? nextIndicatorPoint.Bar.Close : average;
      currentPoint.Series[Name].Time = currentPoint.Time;
      currentPoint.Series[Name].View = View;

      Last = Bar.Close = currentPoint.Series[Name].Bar.Close;

      return this;
    }
  }
}
