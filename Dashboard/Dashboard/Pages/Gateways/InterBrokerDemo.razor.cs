using Canvas.Core.Shapes;
using Core.Enums;
using Core.Indicators;
using Core.Models;
using Dashboard.Components;
using InteractiveBrokers;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace Dashboard.Pages.Gateways
{
  public partial class InterBrokerDemo
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
      ["ES"] = new()
      {
        Name = "ESZ5",
        Exchange = "CME",
        Type = InstrumentEnum.Futures,
        TimeFrame = TimeSpan.FromMinutes(1),
        Basis = new Instrument { Name = "ES" }
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
      Adapters["Prime"] = new InterGateway
      {
        Connector = Connector,
        Port = int.Parse(Configuration["InteractiveBrokers:PaperPort"]),
        Account = new()
        {
          Descriptor = Configuration["InteractiveBrokers:PaperAccount"],
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
      var name = instrument.Name;
      var price = instrument.Price;
      var adapter = Adapters["Prime"];
      var account = adapter.Account;
      var orders = (await adapter.GetOrders(new() { Source = false })).Data;
      var positions = (await adapter.GetPositions(new() { Source = false })).Data;

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
