using Core.CollectionSpace;
using Core.ModelSpace;
using System.Linq;

namespace Core.IndicatorSpace
{
  /// <summary>
  /// Implementation
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class ImbalanceIndicator : IndicatorModel<IPointModel, ImbalanceIndicator>
  {
    /// <summary>
    /// Preserve last calculated value
    /// </summary>
    public ITimeSpanCollection<IPointModel> Values { get; private set; } = new TimeSpanCollection<IPointModel>();

    /// <summary>
    /// Calculate indicator value
    /// </summary>
    /// <param name="currentPoint"></param>
    /// <returns></returns>
    public ImbalanceIndicator Calculate(ITimeCollection<IPointModel> collection, int direction = 0)
    {
      var currentPoint = collection.ElementAtOrDefault(collection.Count - 1);

      if (currentPoint == null)
      {
        return this;
      }

      currentPoint.Series[Name] = currentPoint.Series.TryGetValue(Name, out IPointModel seriesItem) ? seriesItem : new ImbalanceIndicator();
      currentPoint.Series[Name].Time = currentPoint.Time;
      currentPoint.Series[Name].TimeFrame = currentPoint.TimeFrame;
      currentPoint.Series[Name].Chart = Chart;

      var value = 0.0;

      switch (direction)
      {
        case 0: value = currentPoint.AskSize.Value - currentPoint.BidSize.Value; break;
        case 1: value = currentPoint.AskSize.Value; break;
        case -1: value = currentPoint.BidSize.Value; break;
      }

      currentPoint.Series[Name].Last = (currentPoint.Series[Name].Last ?? 0.0) + value;
      currentPoint.Series[Name].Bar.Close = (currentPoint.Series[Name].Bar.Close ?? 0.0) + value;

      Last = Bar.Close = currentPoint.Series[Name].Bar.Close;

      // Save values

      var nextIndicatorPoint = new PointModel
      {
        Last = Last,
        Time = currentPoint.Time,
        TimeFrame = currentPoint.TimeFrame,
        Bar = new PointBarModel
        {
          Close = Last
        }
      };

      Values.Add(nextIndicatorPoint, nextIndicatorPoint.TimeFrame);

      return this;
    }
  }
}
