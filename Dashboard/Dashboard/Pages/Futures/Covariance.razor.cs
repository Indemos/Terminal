using Canvas.Core.Shapes;
using Core.Enums;
using Core.Indicators;
using Core.Models;
using Core.Services;
using Dashboard.Components;
using Simulation;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dashboard.Pages.Futures
{
  public class Indexer : List<(long, double)>
  {
    public new void Add((long, double) item)
    {
      if (Count is 0 || item.Item1 > this[^1].Item1)
      {
        base.Add(item);
      }

      this[^1] = item;
    }
  }

  public partial class Covariance
  {
    ControlsComponent View { get; set; }
    ChartsComponent DataView { get; set; }
    ChartsComponent ScoreView { get; set; }
    ChartsComponent IndicatorsView { get; set; }
    ChartsComponent PerformanceView { get; set; }
    TransactionsComponent TransactionsView { get; set; }
    OrdersComponent OrdersView { get; set; }
    PositionsComponent PositionsView { get; set; }
    StatementsComponent StatementsView { get; set; }
    PerformanceIndicator Performance { get; set; }
    Dictionary<string, ScaleIndicator> Scales { get; set; }

    int Direction { get; set; } = 0;
    double Deviation { get; set; } = 2;
    Indexer Scores { get; set; } = new();
    AverageService AverageService { get; set; } = new();

    const string nameX = "ESU25";
    const string nameY = "NQU25";

    Dictionary<string, Instrument> Instruments = new()
    {
      [nameX] = new() { Name = nameX, StepValue = 12.50, StepSize = 0.25, Leverage = 50, Commission = 3.65 },
      [nameY] = new() { Name = nameY, StepValue = 5, StepSize = 0.25, Leverage = 20, Commission = 3.65 },
    };

    protected override async Task OnView()
    {
      await DataView.Create(nameof(DataView));
      await ScoreView.Create(nameof(ScoreView));
      await IndicatorsView.Create(nameof(IndicatorsView));
      await PerformanceView.Create(nameof(PerformanceView));

      DataView.Composers.ForEach(o => o.ShowIndex = i => GetDate(o.Items, (int)i));
      ScoreView.Composers.ForEach(o => o.ShowIndex = i => GetDate(o.Items, (int)i));
      IndicatorsView.Composers.ForEach(o => o.ShowIndex = i => GetDate(o.Items, (int)i));
      PerformanceView.Composers.ForEach(o => o.ShowIndex = i => GetDate(o.Items, (int)i));
    }

    protected override Task OnTrade()
    {
      var adapter = Adapter = new SimGateway
      {
        Connector = Connector,
        Source = Configuration["Documents:Resources"] + "/FUTS/2025-06-17",
        Account = new()
        {
          Descriptor = "Demo",
          Balance = 25000,
          Instruments = Instruments
        }
      };

      Performance = new PerformanceIndicator { Name = "Balance" };
      Scales = adapter.Account.Instruments.Keys.ToDictionary(o => o, name => new ScaleIndicator
      {
        Name = name,
        Min = -1,
        Max = 1
      });

      return base.OnTrade();
    }

    protected override async Task OnViewUpdate(Instrument instrument)
    {
      var adapter = Adapter;
      var account = adapter.Account;
      var price = instrument.Price;
      var index = price.Bar.Time.Value;
      var assetX = account.Instruments[nameX];
      var assetY = account.Instruments[nameY];
      var seriesX = (await adapter.GetPrices(new() { Count = 100, Instrument = assetX })).Data;
      var seriesY = (await adapter.GetPrices(new() { Count = 100, Instrument = assetY })).Data;

      if (seriesX.Count is 0 || seriesY.Count is 0)
      {
        return;
      }

      var performance = await Performance.Update([adapter]);
      var scaleX = await Scales[assetX.Name].Update(seriesX);
      var scaleY = await Scales[assetY.Name].Update(seriesY);
      var priceX = seriesX.Last();
      var priceY = seriesY.Last();
      var retSeriesX = seriesX.Select(o => o.Last.Value * assetX.Leverage.Value).ToArray();
      var retSeriesY = seriesY.Select(o => o.Last.Value * assetY.Leverage.Value).ToArray();
      var beta = CalculateHedgeRatio(retSeriesX, retSeriesY);
      var score = CalculateZScore(retSeriesX, retSeriesY, beta, (priceX.Last * assetX.Leverage.Value - beta * priceY.Last * assetY.Leverage.Value).Value);

      OrdersView.Update(Adapters.Values);
      PositionsView.Update(Adapters.Values);
      TransactionsView.Update(Adapters.Values);
      ScoreView.Update(index, nameof(ScoreView), "Score", new AreaShape { Y = score, Component = ComUp });
      DataView.Update(index, nameof(DataView), "Leader", new AreaShape { Y = priceX.Last, Component = ComUp });
      IndicatorsView.Update(index, nameof(IndicatorsView), "X", new LineShape { Y = scaleX.Response.Last, Component = ComUp });
      IndicatorsView.Update(index, nameof(IndicatorsView), "Y", new LineShape { Y = scaleY.Response.Last, Component = ComDown });
      PerformanceView.Update(index, nameof(PerformanceView), "Balance", new AreaShape { Y = account.Balance + account.Performance });
      PerformanceView.Update(index, nameof(PerformanceView), "PnL", PerformanceView.GetShape<LineShape>(performance.Response, SKColors.OrangeRed));
    }

    protected override async Task OnTradeUpdate(Instrument instrument)
    {
      if (Equals(instrument.Name, nameX) is false)
      {
        return;
      }

      var price = instrument.Price;
      var adapter = Adapter;
      var account = adapter.Account;
      var assetX = account.Instruments[nameX];
      var assetY = account.Instruments[nameY];
      var seriesX = (await adapter.GetPrices(new() { Count = 100, Instrument = assetX })).Data;
      var seriesY = (await adapter.GetPrices(new() { Count = 100, Instrument = assetY })).Data;

      if (seriesX.Count is 0 || seriesY.Count is 0)
      {
        return;
      }

      var orders = (await adapter.GetOrders(default)).Data;
      var positions = (await adapter.GetPositions(default)).Data;
      var priceX = seriesX.Last();
      var priceY = seriesY.Last();

      if (orders.Count is 0)
      {
        var retSeriesX = seriesX.Select(o => o.Last.Value * assetX.Leverage.Value).ToArray();
        var retSeriesY = seriesY.Select(o => o.Last.Value * assetY.Leverage.Value).ToArray();
        var beta = CalculateHedgeRatio(retSeriesX, retSeriesY);
        var score = CalculateZScore(retSeriesX, retSeriesY, beta, (priceX.Last * assetX.Leverage.Value - beta * priceY.Last * assetY.Leverage.Value).Value);

        Scores.Add((price.Bar.Time.Value, score));

        var isLong = score < -Deviation;
        var isShort = score > Deviation;

        if (positions.Count is 0)
        {
          switch (true)
          {
            case true when isLong:
              Direction = 1;
              await OpenPosition(adapter, assetX, OrderSideEnum.Long);
              await OpenPosition(adapter, assetY, OrderSideEnum.Short);
              break;

            case true when isShort:
              Direction = -1;
              await OpenPosition(adapter, assetX, OrderSideEnum.Short);
              await OpenPosition(adapter, assetY, OrderSideEnum.Long);
              break;
          }
        }

        if (positions.Count is not 0)
        {
          var prevScore = Scores.ElementAtOrDefault(Scores.Count - 1).Item2;
          var closeLong = Direction is 1 && prevScore > 0;
          var closeShort = Direction is -1 && prevScore < 0;

          if (closeLong || closeShort)
          {
            await ClosePosition(adapter);
          }
        }
      }
    }

    /// <summary>
    /// Calculates the Hedge Ratio (Beta) between two time series (Y vs X) using simple linear regression.
    /// This should be calculated over a long look-back window (e.g., 252 daily bars).
    /// </summary>
    /// <param name="y">The time series of the dependent asset (Asset 1, the one being hedged).</param>
    /// <param name="x">The time series of the independent asset (Asset 2, the hedging instrument).</param>
    /// <returns>The Hedge Ratio (Beta) - the number of units of X required to hedge 1 unit of Y.</returns>
    public static double CalculateHedgeRatio(double[] y, double[] x)
    {
      if (y == null || x == null || y.Length != x.Length || y.Length < 2)
      {
        return 0;
      }

      int n = y.Length;

      // 1. Calculate Averages
      double avgY = y.Average();
      double avgX = x.Average();

      // 2. Calculate Numerator (Covariance equivalent: Sum of (xi - avgX) * (yi - avgY))
      double numerator = 0;
      for (int i = 0; i < n; i++)
      {
        numerator += (x[i] - avgX) * (y[i] - avgY);
      }

      // 3. Calculate Denominator (Variance equivalent: Sum of (xi - avgX)^2)
      double denominator = 0;
      for (int i = 0; i < n; i++)
      {
        denominator += Math.Pow(x[i] - avgX, 2);
      }

      // 4. Calculate Beta (Hedge Ratio) = Covariance(X, Y) / Variance(X)
      if (denominator == 0)
      {
        // Avoid division by zero, which implies no variation in the hedging asset price.
        return 0.0;
      }

      return numerator / denominator;
    }

    /// <summary>
    /// Calculates the Z-Score for the current spread relative to its historical mean and standard deviation.
    /// This should be calculated over a shorter, rolling look-back window (e.g., 60-90 bars)
    /// using the spread history that was already calculated using the Hedge Ratio.
    /// </summary>
    /// <param name="spreadHistory">Historical values of the calculated spread (Spread = Y - Beta * X).</param>
    /// <param name="currentSpread">The current calculated value of the spread.</param>
    /// <returns>The Z-score: the number of standard deviations the current spread is from the mean.</returns>
    public static double CalculateZScore(double[] yHistory, double[] xHistory, double beta, double currentSpread)
    {
      if (yHistory == null || xHistory == null || yHistory.Length != xHistory.Length || yHistory.Length < 2)
      {
        return 0;
      }

      // 1. Calculate the historical spread internally: Spread = Y - Beta * X
      List<double> spreadHistory = new List<double>();
      for (int i = 0; i < yHistory.Length; i++)
      {
        spreadHistory.Add(yHistory[i] - beta * xHistory[i]);
      }

      // Now, proceed with mean and standard deviation calculation based on the generated spreadHistory
      int n = spreadHistory.Count;

      // 1. Calculate the Mean (Average) of the historical spread
      double mean = spreadHistory.Average();

      // 2. Calculate the Standard Deviation of the historical spread
      double sumOfSquares = 0;

      foreach (var spread in spreadHistory)
      {
        sumOfSquares += Math.Pow(spread - mean, 2);
      }

      // Use N-1 for sample standard deviation, though N is also common in finance.
      double variance = sumOfSquares / (n - 1);
      double standardDeviation = Math.Sqrt(variance);

      // 3. Calculate the Z-Score
      if (standardDeviation == 0)
      {
        // Spread is perfectly flat, Z-score is undefined or 0.
        return 0.0;
      }

      return (currentSpread - mean) / standardDeviation;
    }
  }
}
