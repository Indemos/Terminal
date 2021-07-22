using System;
using System.Collections.Generic;
using System.Linq;

namespace ScoreSpace
{
  /// <summary>
  /// Generic position model
  /// </summary>
  public class InputData
  {
    public virtual int Direction { get; set; }
    public virtual double Min { get; set; }
    public virtual double Max { get; set; }
    public virtual double Value { get; set; }
    public virtual double Commission { get; set; }
    public virtual DateTime Time { get; set; }
  }

  /// <summary>
  /// Output model
  /// </summary>
  public class ScoreData
  {
    public virtual double Value { get; set; }
    public virtual string Name { get; set; }
    public virtual string Group { get; set; }
    public virtual string Description { get; set; }
  }

  /// <summary>
  /// Stats grouped by position side
  /// </summary>
  public class DirectionData
  {
    public virtual int GainsCount { get; set; }
    public virtual int LossesCount { get; set; }
    public virtual double Gains { get; set; }
    public virtual double Losses { get; set; }
    public virtual double GainsMax { get; set; }
    public virtual double LossesMax { get; set; }
    public virtual double Commissions { get; set; }
  }

  /// <summary>
  /// Common statistical characteristics
  /// </summary>
  public class Metrics
  {
    /// <summary>
    /// Input values
    /// </summary>
    public virtual IEnumerable<InputData> Values { get; set; } = new List<InputData>();

    /// <summary>
    /// Statistics grouped by time frames
    /// </summary>
    public virtual FrameResponse StatsByFrames { get; protected set; } = new FrameResponse();

    /// <summary>
    /// Statistics grouped by series
    /// </summary>
    public virtual SeriesResponse StatsBySeries { get; protected set; } = new SeriesResponse();

    /// <summary>
    /// Calculate
    /// </summary>
    /// <returns></returns>
    public virtual IDictionary<string, IEnumerable<ScoreData>> Calculate()
    {
      if (Values.Any() == false)
      {
        return new Dictionary<string, IEnumerable<ScoreData>>();
      }

      var balanceMax = 0.0;
      var balanceDrawdown = 0.0;
      var balanceDrawdownMax = 0.0;
      var count = Values.Count();
      var longs = new DirectionData();
      var shorts = new DirectionData();
      var inputBalance = Values.ElementAtOrDefault(0);
      var outputBalance = Values.ElementAtOrDefault(count - 1);

      for (var i = 0; i < count; i++)
      {
        var current = Values.ElementAtOrDefault(i);
        var previous = Values.ElementAtOrDefault(i - 1);

        if (current != null && previous != null)
        {
          var item = current.Value - previous.Value;
          var itemGain = Math.Abs(Math.Max(item, 0.0));
          var itemLoss = Math.Abs(Math.Min(item, 0.0));

          balanceDrawdownMax = Math.Max(balanceMax - current.Min, balanceDrawdownMax);
          balanceDrawdown = Math.Max(balanceMax - current.Value, balanceDrawdown);
          balanceMax = Math.Max(balanceMax, current.Value);

          switch (current.Direction)
          {
            case 1:

              longs.Gains += itemGain;
              longs.Losses += itemLoss;
              longs.GainsMax = Math.Max(itemGain, longs.GainsMax);
              longs.LossesMax = Math.Max(itemLoss, longs.LossesMax);
              longs.GainsCount += item > 0.0 ? 1 : 0;
              longs.LossesCount += item < 0.0 ? 1 : 0;
              longs.Commissions += current.Commission;
              break;

            case -1:

              shorts.Gains += itemGain;
              shorts.Losses += itemLoss;
              shorts.GainsMax = Math.Max(itemGain, shorts.GainsMax);
              shorts.LossesMax = Math.Max(itemLoss, shorts.LossesMax);
              shorts.GainsCount += item > 0.0 ? 1 : 0;
              shorts.LossesCount += item < 0.0 ? 1 : 0;
              shorts.Commissions += current.Commission;
              break;
          }
        }
      }

      var stats = new List<ScoreData>();

      stats.Add(new ScoreData { Group = "Balance", Name = "Initial balance $", Value = inputBalance.Value });
      stats.Add(new ScoreData { Group = "Balance", Name = "Final balance $", Value = outputBalance.Value });
      stats.Add(new ScoreData { Group = "Balance", Name = "Commissions $", Value = longs.Commissions + shorts.Commissions });
      stats.Add(new ScoreData { Group = "Balance", Name = "Drawdown $", Value = -balanceDrawdown });
      stats.Add(new ScoreData { Group = "Balance", Name = "Drawdown %", Value = -Validate(() => balanceDrawdown * 100.0 / balanceMax) });
      stats.Add(new ScoreData { Group = "Balance", Name = "Equity drawdown $", Value = -balanceDrawdownMax });
      stats.Add(new ScoreData { Group = "Balance", Name = "Equity drawdown %", Value = -Validate(() => balanceDrawdownMax * 100.0 / balanceMax) });
      stats.Add(new ScoreData { Group = "Balance", Name = "Change $", Value = new Change { Values = Values }.Calculate(0) });
      stats.Add(new ScoreData { Group = "Balance", Name = "Change %", Value = new Change { Values = Values }.Calculate(1) });

      var gains = longs.Gains + shorts.Gains;
      var losses = longs.Losses + shorts.Losses;
      var gainsMax = Math.Max(longs.GainsMax, shorts.GainsMax);
      var lossesMax = Math.Max(longs.LossesMax, shorts.LossesMax);
      var gainsCount = longs.GainsCount + shorts.GainsCount;
      var lossesCount = longs.LossesCount + shorts.LossesCount;
      var gainsAverage = Validate(() => gains / gainsCount);
      var lossesAverage = Validate(() => losses / lossesCount);
      var dealsCount = gainsCount + lossesCount;

      stats.Add(new ScoreData { Group = "Wins", Name = "Total gain $", Value = gains });
      stats.Add(new ScoreData { Group = "Wins", Name = "Max single gain $", Value = gainsMax });
      stats.Add(new ScoreData { Group = "Wins", Name = "Average gain $", Value = gainsAverage });
      stats.Add(new ScoreData { Group = "Wins", Name = "Number of wins", Value = gainsCount });
      stats.Add(new ScoreData { Group = "Wins", Name = "Percentage of wins %", Value = Validate(() => gainsCount * 100.0 / dealsCount) });

      stats.Add(new ScoreData { Group = "Losses", Name = "Total loss $", Value = -losses });
      stats.Add(new ScoreData { Group = "Losses", Name = "Max single loss $", Value = -lossesMax });
      stats.Add(new ScoreData { Group = "Losses", Name = "Average loss $", Value = -lossesAverage });
      stats.Add(new ScoreData { Group = "Losses", Name = "Number of losses", Value = lossesCount });
      stats.Add(new ScoreData { Group = "Losses", Name = "Percentage of losses %", Value = Validate(() => lossesCount * 100.0 / dealsCount) });

      stats.Add(new ScoreData { Group = "Longs", Name = "Total gain $", Value = longs.Gains });
      stats.Add(new ScoreData { Group = "Longs", Name = "Total loss $", Value = -longs.Losses });
      stats.Add(new ScoreData { Group = "Longs", Name = "Max gain $", Value = longs.GainsMax });
      stats.Add(new ScoreData { Group = "Longs", Name = "Max loss $", Value = -longs.LossesMax });
      stats.Add(new ScoreData { Group = "Longs", Name = "Average gain $", Value = Validate(() => longs.Gains / longs.GainsCount) });
      stats.Add(new ScoreData { Group = "Longs", Name = "Average loss $", Value = -Validate(() => longs.Losses / longs.LossesCount) });
      stats.Add(new ScoreData { Group = "Longs", Name = "Number of longs", Value = longs.GainsCount + longs.LossesCount });
      stats.Add(new ScoreData { Group = "Longs", Name = "Percentage of longs %", Value = Validate(() => (longs.GainsCount + longs.LossesCount) * 100.0 / dealsCount) });

      stats.Add(new ScoreData { Group = "Shorts", Name = "Total gain $", Value = shorts.Gains });
      stats.Add(new ScoreData { Group = "Shorts", Name = "Total loss $", Value = -shorts.Losses });
      stats.Add(new ScoreData { Group = "Shorts", Name = "Max gain $", Value = shorts.GainsMax });
      stats.Add(new ScoreData { Group = "Shorts", Name = "Max loss $", Value = -shorts.LossesMax });
      stats.Add(new ScoreData { Group = "Shorts", Name = "Average gain $", Value = Validate(() => shorts.Gains / shorts.GainsCount) });
      stats.Add(new ScoreData { Group = "Shorts", Name = "Average loss $", Value = -Validate(() => shorts.Losses / shorts.LossesCount) });
      stats.Add(new ScoreData { Group = "Shorts", Name = "Number of shorts", Value = shorts.GainsCount + shorts.LossesCount });
      stats.Add(new ScoreData { Group = "Shorts", Name = "Percentage of shorts %", Value = Validate(() => (shorts.GainsCount + shorts.LossesCount) * 100.0 / dealsCount) });

      stats.Add(new ScoreData { Group = "Ratios", Name = "Profit Factor", Value = Validate(() => gains / losses) });
      stats.Add(new ScoreData { Group = "Ratios", Name = "Mean", Value = Validate(() => (gains - losses) / dealsCount) });
      stats.Add(new ScoreData { Group = "Ratios", Name = "CAGR", Value = new CAGR { Values = Values }.Calculate() });
      stats.Add(new ScoreData { Group = "Ratios", Name = "Sharpe Ratio", Value = new SharpeRatio { Values = Values }.Calculate() });
      stats.Add(new ScoreData { Group = "Ratios", Name = "MAE", Value = new MAE { Values = Values }.Calculate() });
      stats.Add(new ScoreData { Group = "Ratios", Name = "MFE", Value = new MFE { Values = Values }.Calculate() });
      stats.Add(new ScoreData { Group = "Ratios", Name = "MAR", Value = new MAR { Values = Values }.Calculate() });
      stats.Add(new ScoreData { Group = "Ratios", Name = "AHPR", Value = new AHPR { Values = Values }.Calculate() });
      stats.Add(new ScoreData { Group = "Ratios", Name = "GHPR", Value = new GHPR { Values = Values }.Calculate() });
      stats.Add(new ScoreData { Group = "Ratios", Name = "Z-Score", Value = new StandardScore { Values = Values }.Calculate() });
      stats.Add(new ScoreData { Group = "Ratios", Name = "E-Ratio", Value = new EdgeRatio { Values = Values }.Calculate() });
      stats.Add(new ScoreData { Group = "Ratios", Name = "Martin Ratio", Value = new MartinRatio { Values = Values }.Calculate() });
      stats.Add(new ScoreData { Group = "Ratios", Name = "Sortino Ratio", Value = new SortinoRatio { Values = Values }.Calculate() });
      stats.Add(new ScoreData { Group = "Ratios", Name = "Sterling Ratio", Value = new SterlingRatio { Values = Values }.Calculate() });
      stats.Add(new ScoreData { Group = "Ratios", Name = "Kestner Ratio", Value = new KestnerRatio { Values = Values }.Calculate() });
      stats.Add(new ScoreData { Group = "Ratios", Name = "LR Correlation", Value = new RegressionCorrelation { Values = Values }.Calculate() });

      StatsByFrames = new FrameMetrics { Values = Values }.Calculate();
      StatsBySeries = new SeriesMetrics { Values = Values }.Calculate();

      return stats.GroupBy(o => o.Group).ToDictionary(o => o.Key, o => o.AsEnumerable());
    }

    /// <summary>
    /// Validate correctness of the expression, e.g. division by zero
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static double Validate(Func<double?> input)
    {
      var output = input();

      if (output == null || double.IsNaN(output.Value) || double.IsInfinity(output.Value))
      {
        return 0.0;
      }

      return output.Value;
    }
  }
}
