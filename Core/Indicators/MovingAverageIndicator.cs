using Distribution.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Terminal.Core.Domains;
using Terminal.Core.Extensions;
using Terminal.Core.Models;
using Terminal.Core.Services;

namespace Terminal.Core.Indicators
{
    /// <summary>
    /// Calculation mode
    /// </summary>
    public enum AveragePriceEnum : byte
  {
    Bid = 1,
    Ask = 2,
    Close = 3
  }

  /// <summary>
  /// Implementation
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class MovingAverageIndicator : Indicator<MovingAverageIndicator>
  {
    /// <summary>
    /// Number of bars to average
    /// </summary>
    public int Interval { get; set; }

    /// <summary>
    /// Calculation mode
    /// </summary>
    public AveragePriceEnum Mode { get; set; }

    /// <summary>
    /// Preserve last calculated value
    /// </summary>
    public IList<double> Values { get; protected set; } = new List<double>();

    /// <summary>
    /// Calculate single value
    /// </summary>
    /// <param name="collection"></param>
    /// <returns></returns>
    public override MovingAverageIndicator Calculate(ObservableCollection<PointModel?> collection)
    {
      var currentPoint = collection.LastOrDefault();

      if (currentPoint is null)
      {
        return this;
      }

      var value = currentPoint?.Price ?? 0;
      var comService = InstanceService<AverageService>.Instance;

      switch (Mode)
      {
        case AveragePriceEnum.Bid: value = currentPoint?.Bid ?? 0; break;
        case AveragePriceEnum.Ask: value = currentPoint?.Ask ?? 0; break;
      }

      switch (Values.Count < collection.Count)
      {
        case true: Values.Add(value); break;
        case false: Values[collection.Count - 1] = value; break;
      }

      var average = comService.LinearWeightAverage(Values, Values.Count - 1, Interval);
      var point = Point ?? new PointModel();

      point.Price = average.IsEqual(0) ? value : average;
      Point = point;

      return this;
    }
  }
}
