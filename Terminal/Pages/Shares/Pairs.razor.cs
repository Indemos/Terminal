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
using Terminal.Core.Indicators;
using Terminal.Core.Models;

namespace Terminal.Pages.Shares
{
  public partial class Pairs
  {
    [Inject] IConfiguration Configuration { get; set; }

    /// <summary>
    /// Strategy
    /// </summary>
    const string _assetX = "GOOG";
    const string _assetY = "GOOGL";

    protected virtual PageComponent View { get; set; }
    protected virtual PerformanceIndicator Performance { get; set; }

    protected override async Task OnAfterRenderAsync(bool setup)
    {
      if (setup)
      {
        await CreateViews();

        View.OnPreConnect = CreateAccounts;
        View.OnPostConnect = () =>
        {
          var account = View.Adapters["Sim"].Account;

          View.DealsView.UpdateItems(account.Deals);
          View.OrdersView.UpdateItems(account.Orders.Values);
          View.PositionsView.UpdateItems(account.Positions.Values);
        };
      }

      await base.OnAfterRenderAsync(setup);
    }

    protected virtual async Task CreateViews()
    {
      var indAreas = new Shape();
      var indCharts = new Shape();

      indCharts.Groups["Range"] = new AreaShape { Component = new ComponentModel { Color = SKColors.DeepSkyBlue } };
      indAreas.Groups["Prices"] = indCharts;

      await View.ChartsView.Create(indAreas);

      var pnlGain = new ComponentModel { Color = SKColors.OrangeRed, Size = 2 };
      var pnlBalance = new ComponentModel { Color = SKColors.Black };
      var pnlAreas = new Shape();
      var pnlCharts = new Shape();

      pnlCharts.Groups["Balance"] = new AreaShape { Component = pnlBalance };
      pnlCharts.Groups["PnL"] = new LineShape { Component = pnlGain };
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

      View.Adapters["Sim"] = new Adapter
      {
        Speed = 1,
        Account = account,
        Source = Configuration["Simulation:Source"]
      };

      Performance = new PerformanceIndicator { Name = "Balance" };

      View
        .Adapters
        .Values
        .ForEach(adapter => adapter.PointStream += async message => await OnData(message.Next));
    }

    protected async Task OnData(PointModel point)
    {
      var adapter = View.Adapters["Sim"];
      var account = adapter.Account;
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

      if (account.Positions.Count == 2)
      {
        var buy = account.Positions.First(o => o.Value.Side == OrderSideEnum.Buy);
        var sell = account.Positions.First(o => o.Value.Side == OrderSideEnum.Sell);
        var gain = buy.Value.GetPointsEstimate() + sell.Value.GetPointsEstimate();

        switch (true)
        {
          case true when gain > expenses: await ClosePositions(); break;
          case true when gain < -expenses: OpenPositions(buy.Value.Transaction.Instrument, sell.Value.Transaction.Instrument); break;
        }
      }

      if (account.Positions.Count is 0)
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
      View.DealsView.UpdateItems(account.Deals);
      View.OrdersView.UpdateItems(account.Orders.Values);
      View.PositionsView.UpdateItems(account.Positions.Values);
    }

    protected void OpenPositions(InstrumentModel assetBuy, InstrumentModel assetSell)
    {
      var adapter = View.Adapters["Sim"];
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

      adapter.CreateOrders(orderBuy, orderSell);
    }

    protected async Task ClosePositions()
    {
      var adapter = View.Adapters["Sim"];

      foreach (var position in adapter.Account.Positions.ToList())
      {
        var side = OrderSideEnum.Buy;

        if (position.Value.Side is OrderSideEnum.Buy)
        {
          side = OrderSideEnum.Sell;
        }

        var order = new OrderModel
        {
          Side = side,
          Type = OrderTypeEnum.Market,
          Transaction = new()
          {
            Volume = position.Value.Transaction.Volume,
            Instrument = position.Value.Transaction.Instrument
          }
        };

        await adapter.CreateOrders(order);
      }
    }
  }
}
