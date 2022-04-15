using Terminal.Core.CollectionSpace;
using Terminal.Core.ModelSpace;
using System.Linq;

namespace Terminal.Core.IndicatorSpace
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
    public IIndexCollection<IPointModel> Values { get; private set; } = new IndexCollection<IPointModel>();

    /// <summary>
    /// Calculate indicator value
    /// </summary>
    /// <param name="currentPoint"></param>
    /// <returns></returns>
    public ImbalanceIndicator Calculate(IIndexCollection<IPointModel> collection, int direction = 0)
    {
      var currentPoint = collection.ElementAtOrDefault(collection.Count - 1);

      if (currentPoint == null)
      {
        return this;
      }

      currentPoint.Series[Name] = currentPoint.Series.TryGetValue(Name, out IPointModel seriesItem) ? seriesItem : new ImbalanceIndicator();
      currentPoint.Series[Name].Time = currentPoint.Time;
      currentPoint.Series[Name].TimeFrame = currentPoint.TimeFrame;
      currentPoint.Series[Name].View = View;

      var value = 0.0;

      switch (direction)
      {
        case 0: value = currentPoint.AskSize.Value - currentPoint.BidSize.Value; break;
        case 1: value = currentPoint.AskSize.Value; break;
        case -1: value = currentPoint.BidSize.Value; break;
      }

      currentPoint.Series[Name].Last = value;
      currentPoint.Series[Name].Bar.Close = value;

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

      var previousIndicatorPoint = Values.ElementAtOrDefault(collection.Count - 1);

      if (previousIndicatorPoint == null)
      {
        Values.Add(nextIndicatorPoint);
      }

      Values[collection.Count - 1] = nextIndicatorPoint;

      return this;
    }
  }
}
