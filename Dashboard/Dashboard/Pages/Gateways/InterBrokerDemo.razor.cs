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
      View.Adapters["Prime"] = new InterGateway
      {
        Messenger = Messenger,
        Connector = Connector,
        Port = int.Parse(Configuration["InteractiveBrokers:PaperPort"]),
        Account = new()
        {
          Name = Configuration["InteractiveBrokers:PaperAccount"],
          Instruments = Instruments
        }
      };

      return base.OnTrade();
    }

    protected override async Task OnViewUpdate(Instrument instrument)
    {
      var name = instrument.Name;
      var price = instrument.Price;
      var adapter = View.Adapters["Prime"];
      var account = adapter.Account;
      var performance = await Performance.Update(View.Adapters.Values);

      OrdersView.Update(View.Adapters.Values);
      PositionsView.Update(View.Adapters.Values);
      TransactionsView.Update(View.Adapters.Values);
      DataView.Update(price.Bar.Time.Value, "Prices", "Bars", DataView.GetShape<CandleShape>(price));
      PerformanceView.Update(price.Time.Value, "Performance", "Balance", new AreaShape { Y = account.Balance + account.Performance });
      PerformanceView.Update(price.Time.Value, "Performance", "PnL", PerformanceView.GetShape<LineShape>(performance.Response, SKColors.OrangeRed));
    }

    protected override async Task OnTradeUpdate(Instrument instrument)
    {
      var name = instrument.Name;
      var price = instrument.Price;
      var adapter = View.Adapters["Prime"];
      var account = adapter.Account;
      var orders = (await adapter.GetOrders(default)).Data;
      var positions = (await adapter.GetPositions(default)).Data;
      var performance = await Performance.Update(View.Adapters.Values);

      if (orders.Count is 0 && positions.Count is 0)
      {
        await OpenPositions(instrument, 1);
        await Done(async () =>
        {
          var position = positions
            .Where(o => Equals(o.Operation.Instrument.Name, name))
            .FirstOrDefault();

          if (position is not null)
          {
            await ClosePositions(name, positions);
          }

        }, 10000);
      }
    }

    async Task OpenPositions(Instrument instrument, double direction)
    {
      var price = instrument.Price.Last;
      var adapter = View.Adapters["Prime"];
      var side = direction > 0 ? OrderSideEnum.Long : OrderSideEnum.Short;
      var stopSide = direction < 0 ? OrderSideEnum.Long : OrderSideEnum.Short;

      var TP = new Order
      {
        Amount = 1,
        Side = stopSide,
        Type = OrderTypeEnum.Limit,
        Instruction = InstructionEnum.Brace,
        Price = price + 50 * direction,
        Operation = new() { Instrument = instrument }
      };

      var SL = new Order
      {
        Amount = 1,
        Side = stopSide,
        Type = OrderTypeEnum.Stop,
        Instruction = InstructionEnum.Brace,
        Price = price - 50 * direction,
        Operation = new() { Instrument = instrument }
      };

      var order = new Order
      {
        Amount = 1,
        Side = side,
        Price = price,
        Type = OrderTypeEnum.Market,
        Operation = new() { Instrument = instrument },
        Orders = [SL, TP]
      };

      await adapter.SendOrder(order);
    }

    async Task ClosePositions(string name, IList<Order> positions)
    {
      var adapter = View.Adapters["Prime"];

      foreach (var position in positions.Where(o => Equals(name, o.Operation.Instrument.Name)))
      {
        var side = position.Side is OrderSideEnum.Long ? OrderSideEnum.Short : OrderSideEnum.Long;
        var order = new Order
        {
          Side = side,
          Type = OrderTypeEnum.Market,
          Amount = position.Amount,
          Operation = new()
          {
            Instrument = position.Operation.Instrument
          }
        };

        await adapter.SendOrder(order);
      }
    }

    /// <summary>
    /// Run with delay
    /// </summary>
    /// <param name="action"></param>
    /// <param name="interval"></param>
    /// <returns></returns>
    public static async Task Done(Action action, int interval)
    {
      await Task.Delay(interval);
      action();
    }
  }
}
