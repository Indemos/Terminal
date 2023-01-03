using Terminal.Core.CollectionSpace;
using Terminal.Core.ModelSpace;
using System;
using System.Linq;
using Terminal.Core.ExtensionSpace;
using System.Collections.Generic;

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
    public IList<double> Values { get; protected set; } = new List<double>();

    /// <summary>
    /// Calculate single value
    /// </summary>
    /// <param name="collection"></param>
    /// <returns></returns>
    public override AverageTrueRangeIndicator Calculate(IIndexCollection<IPointModel> collection)
    {
      var currentPoint = collection.ElementAtOrDefault(collection.Count - 1);
      var previousPoint = collection.ElementAtOrDefault(collection.Count - 2);

      if (currentPoint is null || previousPoint is null)
      {
        return this;
      }

      var value =
        Math.Max(currentPoint.Bar.High.Value, previousPoint.Bar.Close.Value) -
        Math.Min(currentPoint.Bar.Low.Value, previousPoint.Bar.Close.Value);

      if (Values.Count > Interval)
      {
        value = (Values.Last() * Math.Max(Interval - 1, 0) + value) / Interval; 
      }

      switch (Values.Count < collection.Count)
      {
        case true: Values.Add(value); break;
        case false: Values[collection.Count - 1] = value; break;
      }

      var series = currentPoint.Series[Name] = currentPoint.Series.Get(Name) ?? new AverageTrueRangeIndicator();

      Last = series.Last = series.Bar.Close = value;

      return this;
    }
  }
}
