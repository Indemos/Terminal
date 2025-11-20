using Canvas.Core.Shapes;
using Core.Enums;
using Core.Indicators;
using Core.Models;
using Dashboard.Components;
using Simulation;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dashboard.Pages.Shares
{
  public partial class Pairs
  {
    const string assetX = "GOOG";
    const string assetY = "GOOGL";

    ControlsComponent View { get; set; }
    ChartsComponent DataView { get; set; }
    ChartsComponent PerformanceView { get; set; }
    OrdersComponent OrdersView { get; set; }
    PositionsComponent PositionsView { get; set; }
    StatementsComponent StatementsView { get; set; }
    TransactionsComponent TransactionsView { get; set; }
    PerformanceIndicator Performance { get; set; }
    Dictionary<string, Instrument> Instruments => new()
    {
      [assetX] = new Instrument { Name = assetX },
      [assetY] = new Instrument { Name = assetY }
    };

    protected override async Task OnView()
    {
      await DataView.Create("Prices");
      await PerformanceView.Create("Performance");
    }

    protected override Task OnTrade()
    {
      Performance = new PerformanceIndicator();
      Adapters["Prime"] = new SimGateway
      {
        Connector = Connector,
        Source = Configuration["Documents:Resources"],
        Account = new Account
        {
          Descriptor = "Demo",
          Balance = 25000,
          Instruments = Instruments
        }
      };

      return base.OnTrade();
    }

    protected override async void OnViewUpdate(Instrument instrument)
    {
      var price = instrument.Price;
      var adapter = Adapters["Prime"];
      var account = adapter.Account;
      var instrumentX = account.Instruments[assetX];
      var instrumentY = account.Instruments[assetY];
      var seriesX = (await adapter.GetPrices(new Criteria { Count = 1, Instrument = instrumentX })).Data;
      var seriesY = (await adapter.GetPrices(new Criteria { Count = 1, Instrument = instrumentY })).Data;
      var performance = await Performance.Update(Adapters.Values);

      if (seriesX.Count is 0 || seriesY.Count is 0)
      {
        return;
      }

      var xPoint = seriesX.Last();
      var yPoint = seriesY.Last();
      var spread = (xPoint.Ask - xPoint.Bid) + (yPoint.Ask - yPoint.Bid);
      var expenses = spread;
      var range = Math.Max(
        (xPoint.Bid - yPoint.Ask - expenses).Value,
        (yPoint.Bid - xPoint.Ask - expenses).Value);

      OrdersView.Update(Adapters.Values);
      PositionsView.Update(Adapters.Values);
      TransactionsView.Update(Adapters.Values);
      DataView.Update(price.Time.Value, "Prices", "Spread", new AreaShape { Y = range, Component = Com });
      PerformanceView.Update(price.Time.Value, "Performance", "Balance", new AreaShape { Y = account.Balance + account.Performance });
      PerformanceView.Update(price.Time.Value, "Performance", "PnL", PerformanceView.GetShape<LineShape>(performance.Response, SKColors.OrangeRed));
    }

    protected override async Task OnTradeUpdate(Instrument instrument)
    {
      var price = instrument.Price;
      var adapter = Adapters["Prime"];
      var account = adapter.Account;
      var instrumentX = account.Instruments[assetX];
      var instrumentY = account.Instruments[assetY];
      var seriesX = (await adapter.GetPrices(new Criteria { Count = 1, Instrument = instrumentX })).Data;
      var seriesY = (await adapter.GetPrices(new Criteria { Count = 1, Instrument = instrumentY })).Data;

      if (seriesX.Count is 0 || seriesY.Count is 0)
      {
        return;
      }

      var orders = (await adapter.GetOrders(default)).Data;
      var positions = (await adapter.GetPositions(default)).Data;
      var xPoint = seriesX.Last();
      var yPoint = seriesY.Last();
      var spread = (xPoint.Ask - xPoint.Bid) + (yPoint.Ask - yPoint.Bid);
      var expenses = spread;
      var posAmount = positions.Sum(o => o.Operation.Amount);

      if (orders.Count is 0)
      {
        var buy = positions.FirstOrDefault(o => o.Side is OrderSideEnum.Long);
        var sell = positions.FirstOrDefault(o => o.Side is OrderSideEnum.Short);

        if (buy is not null && sell is not null)
        {
          var gain = buy.Balance.Current + sell.Balance.Current;

          switch (true)
          {
            case true when gain > expenses * 2: await ClosePosition(adapter); break;
            case true when gain < -expenses * posAmount:
              await OpenPosition(adapter, buy.Operation.Instrument, OrderSideEnum.Long);
              await OpenPosition(adapter, sell.Operation.Instrument, OrderSideEnum.Short);
              break;
          }
        }

        if (positions.Count is 0)
        {
          switch (true)
          {
            case true when (xPoint.Bid - yPoint.Ask) > expenses:
              await OpenPosition(adapter, instrumentY, OrderSideEnum.Long);
              await OpenPosition(adapter, instrumentX, OrderSideEnum.Short);
              break;

            case true when (yPoint.Bid - xPoint.Ask) > expenses:
              await OpenPosition(adapter, instrumentX, OrderSideEnum.Long);
              await OpenPosition(adapter, instrumentY, OrderSideEnum.Short);
              break;
          }
        }
      }
    }
  }
}
