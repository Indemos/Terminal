using System;
using System.Collections.Generic;
using System.Linq;

namespace ScoreSpace
{
  /// <summary>
  /// Single series data
  /// </summary>
  public class SeriesData
  {
    public int Count { get; set; }
    public int WinCount { get; set; }
    public int LossCount { get; set; }
    public int Direction { get; set; }
    public double Gain { get; set; }
    public double Loss { get; set; }
  }

  /// <summary>
  /// Single series data
  /// </summary>
  public class SeriesResponse
  {
    public int Count { get; set; }
    public int MaxWinCount { get; set; }
    public int MaxLossCount { get; set; }
    public double MaxWin { get; set; }
    public double MaxLoss { get; set; }
  }

  /// <summary>
  /// Statistics grouped by series
  /// </summary>
  public class SeriesMetrics
  {
    /// <summary>
    /// Input values
    /// </summary>
    public virtual IEnumerable<InputData> Values { get; set; } = new List<InputData>();

    /// <summary>
    /// Calculate
    /// </summary>
    /// <returns></returns>
    public virtual SeriesResponse Calculate()
    {
      var count = Values.Count();
      var seriesInverse = 0;
      var seriesItem = new SeriesData();
      var seriesItems = new List<SeriesData>();

      for (var i = 0; i < count; i++)
      {
        var current = Values.ElementAtOrDefault(i);
        var previous = Values.ElementAtOrDefault(i - 1);

        if (current != null && previous != null)
        {
          var direction = 0;
          var change = current.Value - previous.Value;
          var gain = Math.Abs(Math.Max(change, 0.0));
          var loss = Math.Abs(Math.Min(change, 0.0));

          direction = previous.Value < current.Value ? 1 : direction;
          direction = previous.Value > current.Value ? -1 : direction;

          seriesItem = UpdateSeries(seriesInverse, direction, seriesItem, seriesItems);
          seriesItem.Gain += gain;
          seriesItem.Loss += loss;
          seriesItem.WinCount += gain > 0 ? 1 : 0;
          seriesItem.LossCount += loss > 0 ? 1 : 0;
          seriesItem.Count++;

          seriesInverse = direction;
        }
      }

      var response = new SeriesResponse
      {
        Count = 0,
        MaxWinCount = 0,
        MaxLossCount = 0,
        MaxWin = 0.0,
        MaxLoss = 0.0
      };

      if (seriesItems.Any())
      {
        response.Count = seriesItems.Count;
        response.MaxWin = seriesItems.Max(o => o.Gain);
        response.MaxLoss = seriesItems.Max(o => o.Loss);
        response.MaxWinCount = seriesItems.Max(o => o.WinCount);
        response.MaxLossCount = seriesItems.Max(o => o.LossCount);
      }

      return response;
    }

    /// <summary>
    /// Compare series direction and add to the list if the series inversed
    /// </summary>
    /// <param name="direction"></param>
    /// <param name="change"></param>
    /// <param name="series"></param>
    /// <param name="items"></param>
    protected SeriesData UpdateSeries(int direction, int change, SeriesData series, List<SeriesData> items)
    {
      if (direction != 0 && change != 0 && direction != change)
      {
        series.Direction = direction;
        items.Add(series);

        return new SeriesData()
        {
          Direction = change
        };
      }

      return series;
    }
  }
}
