using System.Collections.ObjectModel;
using System.Linq;
using Terminal.Core.Domains;
using Terminal.Core.Extensions;
using Terminal.Core.Models;

namespace Terminal.Core.Indicators
{
  /// <summary>
  /// Implementation
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class ImbalanceIndicator : Indicator<ImbalanceIndicator>
  {
    /// <summary>
    /// Calculate indicator value
    /// </summary>
    /// <param name="collection"></param>
    /// <param name="side"></param>
    /// <returns></returns>
    public ImbalanceIndicator Calculate(ObservableCollection<PointModel?> collection, int side = 0)
    {
      var currentPoint = collection.LastOrDefault();

      if (currentPoint is null)
      {
        return this;
      }

      var value = 0.0;
      var bid = currentPoint?.BidSize ?? 0;
      var ask = currentPoint?.AskSize ?? 0;

      switch (side)
      {
        case 0: value = ask - bid; break;
        case 1: value = ask; break;
        case -1: value = bid; break;
      }

      var point = Point ?? new PointModel();

      point.Price = value;
      Point = point;

      return this;
    }
  }
}
