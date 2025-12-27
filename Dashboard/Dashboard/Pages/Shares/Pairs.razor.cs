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
      Adapter = new SimGateway
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

    protected override async Task OnViewUpdate(Instrument instrument)
    {
      var price = instrument.Price;
      var account = Adapter.Account;
      var instrumentX = account.Instruments[assetX];
      var instrumentY = account.Instruments[assetY];
      var seriesX = (await Adapter.GetPrices(new Criteria { Count = 1, Instrument = instrumentX })).Data;
      var seriesY = (await Adapter.GetPrices(new Criteria { Count = 1, Instrument = instrumentY })).Data;
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
      if (Equals(instrument.Name, assetX) is false)
      {
        return;
      }

      var price = instrument.Price;
      var account = Adapter.Account;
      var instrumentX = account.Instruments[assetX];
      var instrumentY = account.Instruments[assetY];
      var seriesX = (await Adapter.GetPrices(new() { Count = 1, Instrument = instrumentX })).Data;
      var seriesY = (await Adapter.GetPrices(new() { Count = 1, Instrument = instrumentY })).Data;

      if (seriesX.Count is 0 || seriesY.Count is 0)
      {
        return;
      }

      var orders = (await Adapter.GetOrders(default)).Data;
      var positions = (await Adapter.GetPositions(default)).Data;
      var xPoint = seriesX.Last();
      var yPoint = seriesY.Last();
      var spread = (xPoint.Ask - xPoint.Bid) + (yPoint.Ask - yPoint.Bid);
      var expenses = spread;
      var posAmount = positions.Sum(o => o.Operation.Amount);

      if (orders.Count is 0)
      {
        var longs = positions.Where(o => o.Side is OrderSideEnum.Long).ToList();
        var shorts = positions.Where(o => o.Side is OrderSideEnum.Short).ToList();

        if (positions.Count is not 0 && longs.Count == shorts.Count)
        {
          var buy = longs.First();
          var sell = shorts.First();
          var gain = buy.Balance.Current + sell.Balance.Current;

          switch (true)
          {
            case true when gain > expenses * 2: await ClosePosition(Adapter); break;
            case true when gain < -expenses * posAmount:
              await OpenPosition(Adapter, buy.Operation.Instrument, OrderSideEnum.Long);
              await OpenPosition(Adapter, sell.Operation.Instrument, OrderSideEnum.Short);
              break;
          }
        }

        if (positions.Count is 0)
        {
          switch (true)
          {
            case true when (xPoint.Bid - yPoint.Ask) > expenses:
              await OpenPosition(Adapter, instrumentY, OrderSideEnum.Long);
              await OpenPosition(Adapter, instrumentX, OrderSideEnum.Short);
              break;

            case true when (yPoint.Bid - xPoint.Ask) > expenses:
              await OpenPosition(Adapter, instrumentX, OrderSideEnum.Long);
              await OpenPosition(Adapter, instrumentY, OrderSideEnum.Short);
              break;
          }
        }
      }
    }
  }
}
