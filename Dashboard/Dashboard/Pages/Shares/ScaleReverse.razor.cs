using Canvas.Core.Shapes;
using Core.Enums;
using Core.Indicators;
using Core.Models;
using Dashboard.Components;
using Simulation;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;

namespace Dashboard.Pages.Shares
{
  public partial class ScaleReverse
  {
    ControlsComponent View { get; set; }
    ChartsComponent DataView { get; set; }
    ChartsComponent PerformanceView { get; set; }
    OrdersComponent OrdersView { get; set; }
    PositionsComponent PositionsView { get; set; }
    StatementsComponent StatementsView { get; set; }
    TransactionsComponent TransactionsView { get; set; }
    PerformanceIndicator Performance { get; set; }

    double? Price { get; set; }
    double Step { get; set; } = 5;
    double StepBase { get; set; } = 5;
    double StepIncrement { get; set; } = -1;
    string Asset { get; set; } = "GOOG";

    Dictionary<string, Instrument> Instruments => new()
    {
      [Asset] = new Instrument { Name = Asset }
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

    protected override async void OnViewUpdate(Instrument instrument)
    {
      var price = instrument.Price;
      var account = Adapter.Account;
      var performance = await Performance.Update(Adapters.Values);

      OrdersView.Update(Adapters.Values);
      PositionsView.Update(Adapters.Values);
      TransactionsView.Update(Adapters.Values);
      DataView.Update(price.Time.Value, "Prices", "Spread", new AreaShape { Y = price.Last, Component = Com });
      PerformanceView.Update(price.Time.Value, "Performance", "Balance", new AreaShape { Y = account.Balance + account.Performance });
      PerformanceView.Update(price.Time.Value, "Performance", "PnL", PerformanceView.GetShape<LineShape>(performance.Response, SKColors.OrangeRed));
    }

    protected override async Task OnTradeUpdate(Instrument instrument)
    {
      var price = instrument.Price;
      var account = Adapter.Account;

      Price ??= price.Last;

      var orders = (await Adapter.GetOrders(default)).Data;
      var positions = (await Adapter.GetPositions(default)).Data;

      if (orders.Count is not 0)
      {
        return;
      }

      if (positions.Count is not 0)
      {
        var pos = positions.First();
        var closures = new List<Order>();
        var isStepUp = price.Last - Price > Step;
        var isStepDown = Price - price.Last > Step;

        if (isStepUp && pos.Side is not OrderSideEnum.Long)
        {
          closures = await ClosePosition(Adapter, o => o.Side is OrderSideEnum.Short);
          await OpenPosition(Adapter, instrument, OrderSideEnum.Long);
        }

        if (isStepDown && pos.Side is not OrderSideEnum.Short)
        {
          closures = await ClosePosition(Adapter, o => o.Side is OrderSideEnum.Long);
          await OpenPosition(Adapter, instrument, OrderSideEnum.Short);
        }

        // Progressive increase

        if (isStepUp || isStepDown)
        {
          Step = closures.Count is not 0 ? StepBase : Math.Max(Math.Abs(StepIncrement), Step + StepIncrement);
          Price = price.Last;
        }
      }

      if (positions.Count is 0)
      {
        await OpenPosition(Adapter, instrument, OrderSideEnum.Short);
      }
    }
  }
}
