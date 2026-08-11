using Canvas.Core.Shapes;
using Core.Enums;
using Core.Indicators;
using Core.Models;
using Dashboard.Components;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Topstep;

namespace Dashboard.Pages.Gateways
{
  public partial class TopstepBrokerDemo
  {
    ChartsComponent DataView { get; set; }
    ChartsComponent PerformanceView { get; set; }
    TransactionsComponent TransactionsView { get; set; }
    OrdersComponent OrdersView { get; set; }
    PositionsComponent PositionsView { get; set; }
    StatementsComponent StatementsView { get; set; }
    PerformanceIndicator Performance { get; set; }
    Dictionary<string, Instrument> Instruments => new()
    {
      ["MES"] = new()
      {
        Name = "MES",
        Id = "CON.F.US.MES.U26",
        Type = InstrumentEnum.Futures,
        TimeFrame = TimeSpan.FromSeconds(1)
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
      Adapter = new TopstepGateway
      {
        Connector = Connector,
        Token = Configuration["Topstep:Token"],
        Username = Configuration["Topstep:Username"],
        Account = new()
        {
          Instruments = Instruments,
          Descriptor = Configuration["Topstep:Account"]
        }
      };

      return base.OnTrade();
    }

    protected async Task Render(Instrument instrument)
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
      var orders = (await adapter.GetOrders(new() { Source = true, Account = account })).Data;
      var positions = (await adapter.GetPositions(new() { Source = true, Account = account })).Data;

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

      await Render(instrument);
    }

    async Task Done(Action action, int interval)
    {
      await Task.Delay(interval);
      action();
    }
  }
}
