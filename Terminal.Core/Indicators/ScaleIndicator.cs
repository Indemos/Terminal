using Terminal.Core.CollectionSpace;
using Terminal.Core.ExtensionSpace;
using Terminal.Core.ModelSpace;
using Terminal.Core.ServiceSpace;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Terminal.Core.IndicatorSpace
{
  /// <summary>
  /// Implementation
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class ScaleIndicator : IndicatorModel<IPointModel, ScaleIndicator>
  {
    /// <summary>
    /// Number of bars to average
    /// </summary>
    public int Interval { get; set; }

    /// <summary>
    /// Bottom border of the normalized series
    /// </summary>
    public double Min { get; set; }

    /// <summary>
    /// Top border of the normalized series
    /// </summary>
    public double Max { get; set; }

    /// <summary>
    /// Preserve last calculated value
    /// </summary>
    public IList<double> Values { get; protected set; } = new List<double>();

    /// <summary>
    /// Preserve last calculated min value
    /// </summary>
    protected double? _min = null;

    /// <summary>
    /// Preserve last calculated max value
    /// </summary>
    protected double? _max = null;

    /// <summary>
    /// Calculate indicator value
    /// </summary>
    /// <param name="collection"></param>
    /// <returns></returns>
    public override ScaleIndicator Calculate(IIndexCollection<IPointModel> collection)
    {
      var currentPoint = collection.LastOrDefault();

      if (currentPoint is null)
      {
        return this;
      }

      var value = currentPoint.Last ?? 0.0;
      var comService = InstanceService<AverageService>.Instance;

      _min = _min is null ? value : Math.Min(_min.Value, value);
      _max = _max is null ? value : Math.Max(_max.Value, value);

      if (_min.Value.IsEqual(_max.Value) is false)
      {
        value = Min + (value - _min.Value) * (Max - Min) / (_max.Value - _min.Value);
      }

      switch (Values.Count < collection.Count)
      {
        case true: Values.Add(value); break;
        case false: Values[collection.Count - 1] = value; break;
      }

      var series = currentPoint.Series[Name] = currentPoint.Series.Get(Name) ?? new ScaleIndicator();

      Last = series.Last = series.Bar.Close = comService.LinearWeightAverage(Values, Values.Count - 1, Interval);

      return this;
    }
  }
}
