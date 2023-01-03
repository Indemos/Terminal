using System.Linq;
using Terminal.Core.CollectionSpace;
using Terminal.Core.ExtensionSpace;
using Terminal.Core.ModelSpace;

namespace Terminal.Core.IndicatorSpace
{
  /// <summary>
  /// Implementation
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class ImbalanceIndicator : IndicatorModel<IPointModel, ImbalanceIndicator>
  {
    /// <summary>
    /// Calculate indicator value
    /// </summary>
    /// <param name="collection"></param>
    /// <param name="side"></param>
    /// <returns></returns>
    public ImbalanceIndicator Calculate(IIndexCollection<IPointModel> collection, int side = 0)
    {
      var currentPoint = collection.LastOrDefault();

      if (currentPoint is null)
      {
        return this;
      }

      var value = 0.0;
      var seriesItem = currentPoint.Series[Name] = currentPoint.Series.Get(Name) ?? new ImbalanceIndicator();

      switch (side)
      {
        case 0: value = currentPoint.AskSize.Value - currentPoint.BidSize.Value; break;
        case 1: value = currentPoint.AskSize.Value; break;
        case -1: value = currentPoint.BidSize.Value; break;
      }

      Last = Bar.Close = seriesItem.Last = seriesItem.Bar.Close = value;

      return this;
    }
  }
}
