using Canvas.Core.Shapes;
using Core.Enums;
using Core.Indicators;
using Core.Models;
using Dashboard.Components;
using Coin;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using CryptoClients.Net.Enums;

namespace Dashboard.Pages.Gateways
{
  public partial class CoinDemo
  {
    ControlsComponent View { get; set; }
    ChartsComponent DataView { get; set; }
    ChartsComponent PerformanceView { get; set; }
    TransactionsComponent TransactionsView { get; set; }
    OrdersComponent OrdersView { get; set; }
    PositionsComponent PositionsView { get; set; }
    StatementsComponent StatementsView { get; set; }
    PerformanceIndicator Performance { get; set; }
    Dictionary<string, Instrument> Instruments => new()
    {
      ["ETH"] = new()
      {
        Name = "ETH",
        Type = InstrumentEnum.Coins,
        TimeFrame = TimeSpan.FromMinutes(1),
        Currency = new() { Name = "USD" }
      }
    };

    protected override async Task OnView()
    {
      await DataView.Create("Prices");
      await PerformanceView.Create("Performance");
    }

    protected override Task OnTrade()
    {
      Performance = new PerformanceIndicator();
      Adapter = new CoinGateway
      {
        Connector = Connector,
        Token = Configuration["Coinbase:Token"],
        Secret = Configuration["Coinbase:Secret"],
        Exchange = Exchange.Coinbase,
        Account = new()
        {
          Descriptor = Configuration["Coinbase:App"],
          Instruments = Instruments
        }
      };

      return base.OnTrade();
    }

    protected override async void OnViewUpdate(Instrument instrument)
    {
      var adapter = Adapter;
      var price = instrument.Price;
      var account = adapter.Account;
      var performance = await Performance.Update(Adapters.Values);

      OrdersView.Update(Adapters.Values);
      PositionsView.Update(Adapters.Values);
      TransactionsView.Update(Adapters.Values);
      DataView.Update(price.Bar.Time.Value, "Prices", "Bars", DataView.GetShape<CandleShape>(price));
      PerformanceView.Update(price.Time.Value, "Performance", "Balance", new AreaShape { Y = account.Balance + account.Performance });
      PerformanceView.Update(price.Time.Value, "Performance", "PnL", PerformanceView.GetShape<LineShape>(performance.Response, SKColors.OrangeRed));
    }

    protected override async Task OnTradeUpdate(Instrument instrument)
    {
      var adapter = Adapter;
      var name = instrument.Name;
      var price = instrument.Price;
      var account = adapter.Account;
      var orders = (await adapter.GetOrders(new() { Source = true })).Data;
      var positions = (await adapter.GetPositions(new() { Source = true })).Data;

      if (orders.Count is 0 && positions.Count is 0)
      {
        await OpenPosition(adapter, instrument, OrderSideEnum.Long);
        await Done(async () =>
        {
          var position = positions
            .Where(o => Equals(o.Operation.Instrument.Name, name))
            .FirstOrDefault();

          if (position is not null)
          {
            await ClosePosition(adapter);
          }

        }, 10000);
      }
    }

    async Task Done(Action action, int interval)
    {
      await Task.Delay(interval);
      action();
    }
  }
}
