using Canvas.Core.Models;
using Canvas.Core.Shapes;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Simulation;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Components;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Extensions;
using Terminal.Core.Indicators;
using Terminal.Core.Models;

namespace Terminal.Pages
{
  public partial class Pairs : IDisposable
  {
    [Inject] IConfiguration Configuration { get; set; }

    /// <summary>
    /// Strategy
    /// </summary>
    const string _assetX = "GOOG";
    const string _assetY = "GOOGL";

    protected virtual PageComponent View { get; set; }
    protected virtual PerformanceIndicator Performance { get; set; }
    protected virtual IGateway Adapter
    {
      get => View.Adapters.Get(string.Empty);
      set => View.Adapters[string.Empty] = value;
    }

    protected override async Task OnAfterRenderAsync(bool setup)
    {
      if (setup)
      {
        await CreateViews();

        View.OnPreConnect = CreateAccounts;
        View.OnPostConnect = () =>
        {
          var account = Adapter.Account;

          View.DealsView.UpdateItems(account.Positions);
          View.OrdersView.UpdateItems(account.ActiveOrders);
          View.PositionsView.UpdateItems(account.ActivePositions);
        };
      }

      await base.OnAfterRenderAsync(setup);
    }

    protected virtual async Task CreateViews()
    {
      var indUp = new ComponentModel { Color = SKColors.DeepSkyBlue };
      var indDown = new ComponentModel { Color = SKColors.OrangeRed };
      var indAreas = new Shape();
      var indCharts = new Shape();

      indCharts.Groups["Ups"] = new ArrowShape { Component = indUp };
      indCharts.Groups["Downs"] = new ArrowShape { Component = indDown };
      indCharts.Groups["Range"] = new AreaShape { Component = indUp };
      indAreas.Groups["Prices"] = indCharts;

      await View.ChartsView.Create(indAreas);

      var pnlGain = new ComponentModel { Color = SKColors.OrangeRed, Size = 5 };
      var pnlBalance = new ComponentModel { Color = SKColors.Black };
      var pnlAreas = new Shape();
      var pnlCharts = new Shape();

      pnlCharts.Groups["PnL"] = new LineShape { Component = pnlGain };
      pnlCharts.Groups["Balance"] = new AreaShape { Component = pnlBalance };
      pnlAreas.Groups["Performance"] = pnlCharts;

      await View.ReportsView.Create(pnlAreas);
    }

    protected virtual void CreateAccounts()
    {
      var account = new Account
      {
        Balance = 25000,
        Instruments = new ConcurrentDictionary<string, InstrumentModel>
        {
          [_assetX] = new InstrumentModel { Name = _assetX },
          [_assetY] = new InstrumentModel { Name = _assetY }
        }
      };

      Adapter = new Adapter
      {
        Speed = 1,
        Account = account,
        Source = Configuration["Simulation:Source"]
      };

      Performance = new PerformanceIndicator { Name = "Balance" };

      account
        .Instruments
        .Values
        .ForEach(o => o.Points.CollectionChanged += (_, e) => e
          .NewItems
          .OfType<PointModel>()
          .ForEach(o => OnData(o)));
    }

    private void OnData(PointModel point)
    {
      var account = Adapter.Account;
      var instrumentX = account.Instruments[_assetX];
      var instrumentY = account.Instruments[_assetY];
      var seriesX = instrumentX.Points;
      var seriesY = instrumentY.Points;

      if (seriesX.Count is 0 || seriesY.Count is 0)
      {
        return;
      }

      var chartPoints = new List<KeyValuePair<string, PointModel>>();
      var reportPoints = new List<KeyValuePair<string, PointModel>>();
      var performance = Performance.Calculate([account]);
      var xPoint = seriesX.Last();
      var yPoint = seriesY.Last();
      var spread = (xPoint.Ask - xPoint.Bid) + (yPoint.Ask - yPoint.Bid);
      var expenses = spread * 2;

      if (account.ActivePositions.Count == 2)
      {
        var buy = account.ActivePositions.First(o => o.Order.Side == OrderSideEnum.Buy);
        var sell = account.ActivePositions.First(o => o.Order.Side == OrderSideEnum.Sell);
        var gain = buy.Order.GetPointsEstimate() + sell.Order.GetPointsEstimate();

        switch (true)
        {
          case true when gain > expenses: ClosePositions(); break;
          case true when gain < -expenses: OpenPositions(buy.Order.Transaction.Instrument, sell.Order.Transaction.Instrument); break;
        }
      }

      if (account.ActivePositions.IsEmpty)
      {
        switch (true)
        {
          case true when (xPoint.Bid - yPoint.Ask) > expenses: OpenPositions(instrumentY, instrumentX); break;
          case true when (yPoint.Bid - xPoint.Ask) > expenses: OpenPositions(instrumentX, instrumentY); break;
        }
      }

      var range = Math.Max(
        (xPoint.Bid - yPoint.Ask - expenses).Value,
        (yPoint.Bid - xPoint.Ask - expenses).Value);

      chartPoints.Add(KeyValuePair.Create("Range", new PointModel { Time = point.Time, Last = Math.Max(0, range) }));
      reportPoints.Add(KeyValuePair.Create("Balance", new PointModel { Time = point.Time, Last = account.Balance }));
      reportPoints.Add(KeyValuePair.Create("PnL", new PointModel { Time = point.Time, Last = performance.Point.Last }));

      View.ChartsView.UpdateItems(chartPoints, 100);
      View.ReportsView.UpdateItems(reportPoints);
      View.DealsView.UpdateItems(account.Positions);
      View.OrdersView.UpdateItems(account.ActiveOrders);
      View.PositionsView.UpdateItems(account.ActivePositions);
    }

    private void OpenPositions(InstrumentModel assetBuy, InstrumentModel assetSell)
    {
      var orderSell = new OrderModel
      {
        Side = OrderSideEnum.Sell,
        Type = OrderTypeEnum.Market,
        Transaction = new()
        {
          Volume = 1,
          Instrument = assetSell
        }
      };

      var orderBuy = new OrderModel
      {
        Side = OrderSideEnum.Buy,
        Type = OrderTypeEnum.Market,
        Transaction = new()
        {
          Volume = 1,
          Instrument = assetBuy
        }
      };

      Adapter.CreateOrders(orderBuy, orderSell);

      var account = Adapter.Account;
      //var buy = account.InternalActivePositions.First(o => o.Order.Side == OrderSideEnum.Buy);
      //var sell = account.InternalActivePositions.First(o => o.Order.Side == OrderSideEnum.Sell);

      //points.Add(new PointModel { Time = buy.Time, Name = nameof(OrderSideEnum.Buy), Last = buy.OpenPrices.Last().Price });
      //points.Add(new PointModel { Time = sell.Time, Name = nameof(OrderSideEnum.Sell), Last = sell.OpenPrices.Last().Price });
    }

    private void ClosePositions()
    {
      foreach (var position in Adapter.Account.ActivePositions)
      {
        var side = OrderSideEnum.Buy;

        if (position.Order.Side is OrderSideEnum.Buy)
        {
          side = OrderSideEnum.Sell;
        }

        var order = new OrderModel
        {
          Side = side,
          Type = OrderTypeEnum.Market,
          Transaction = new()
          {
            Volume = position.Order.Transaction.CurrentVolume,
            Instrument = position.Order.Transaction.Instrument
          }
        };

        Adapter.CreateOrders(order);

        //points.Add(new PointModel { Time = order.Time, Name = nameof(OrderSideEnum.Buy), Last = price });
      }
    }

    public void Dispose() => Adapter?.Disconnect();
  }
}
