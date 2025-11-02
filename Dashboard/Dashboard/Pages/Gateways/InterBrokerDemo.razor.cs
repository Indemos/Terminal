using Canvas.Core.Extensions;
using Canvas.Core.Shapes;
using Core.Conventions;
using Core.Enums;
using Core.Indicators;
using Core.Models;
using Core.Services;
using Dashboard.Components;
using InteractiveBrokers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Orleans;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace Dashboard.Pages.Gateways
{
  public partial class InterBrokerDemo
  {
    [Inject] IClusterClient Connector { get; set; }
    [Inject] IConfiguration Configuration { get; set; }
    [Inject] MessageService Messenger { get; set; }
    [Inject] StateService State { get; set; }

    ControlsComponent View { get; set; }
    ChartsComponent DataView { get; set; }
    ChartsComponent PerformanceView { get; set; }
    TransactionsComponent TransactionsView { get; set; }
    OrdersComponent OrdersView { get; set; }
    PositionsComponent PositionsView { get; set; }
    StatementsComponent StatementsView { get; set; }
    PerformanceIndicator Performance { get; set; }
    Dictionary<string, InstrumentModel> Instruments => new()
    {
      ["ES"] = new()
      {
        Name = "ESZ5",
        Exchange = "CME",
        Type = InstrumentEnum.Futures,
        Basis = new InstrumentModel { Name = "ES" }
      }
    };

    IGateway Adapter
    {
      get => View.Adapters.Get(string.Empty);
      set => View.Adapters[string.Empty] = value;
    }

    protected override async Task OnAfterRenderAsync(bool setup)
    {
      if (setup)
      {
        await DataView.Create("Prices");
        await PerformanceView.Create("Performance");

        Messenger.Subscribe<InstrumentModel>(async o =>
        {
          if (Equals(o.Name, Instruments.First().Key))
          {
            Show(o);
            await Trade(o);
          }
        });

        State.Subscribe(state =>
        {
          if (state.Previous is SubscriptionEnum.None && state.Next is SubscriptionEnum.Progress)
          {
            Performance = new PerformanceIndicator();
            Adapter = new InterGateway
            {
              Messenger = Messenger,
              Connector = Connector,
              Port = int.Parse(Configuration["InteractiveBrokers:PaperPort"]),
              Account = new AccountModel
              {
                Name = Configuration["InteractiveBrokers:PaperAccount"],
                Instruments = Instruments
              }
            };
          }

          return Task.CompletedTask;
        });
      }

      await base.OnAfterRenderAsync(setup);
    }

    void Show(InstrumentModel instrument)
    {
      var name = instrument.Name;
      var price = instrument.Price;
      var account = Adapter.Account;
      //var orders = await Adapter.GetOrders(default);
      //var positions = await Adapter.GetPositions(default);
      //var performance = await Performance.Update(View.Adapters.Values);

      OrdersView.Update(View.Adapters.Values);
      PositionsView.Update(View.Adapters.Values);
      TransactionsView.Update(View.Adapters.Values);
      DataView.Update(price.Bar.Time.Value, "Prices", "Bars", DataView.GetShape<CandleShape>(price));
      PerformanceView.Update(price.Time.Value, "Performance", "Balance", new AreaShape { Y = account.Balance + account.Performance });
      //PerformanceView.Update(price.Time.Value, "Performance", "PnL", PerformanceView.GetShape<LineShape>(performance.Response, SKColors.OrangeRed));
    }

    async Task Trade(InstrumentModel instrument)
    {
      var name = instrument.Name;
      var price = instrument.Price;
      var account = Adapter.Account;
      var orders = await Adapter.GetOrders(default);
      var positions = await Adapter.GetPositions(default);
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

    async Task OpenPositions(InstrumentModel instrument, double direction)
    {
      var price = instrument.Price.Last;
      var adapter = View.Adapters["Prime"];
      var side = direction > 0 ? OrderSideEnum.Long : OrderSideEnum.Short;
      var stopSide = direction < 0 ? OrderSideEnum.Long : OrderSideEnum.Short;

      var TP = new OrderModel
      {
        Amount = 1,
        Side = stopSide,
        Type = OrderTypeEnum.Limit,
        Instruction = InstructionEnum.Brace,
        Price = price + 50 * direction,
        Operation = new() { Instrument = instrument }
      };

      var SL = new OrderModel
      {
        Amount = 1,
        Side = stopSide,
        Type = OrderTypeEnum.Stop,
        Instruction = InstructionEnum.Brace,
        Price = price - 50 * direction,
        Operation = new() { Instrument = instrument }
      };

      var order = new OrderModel
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

    async Task ClosePositions(string name, IList<OrderModel> positions)
    {
      var adapter = View.Adapters["Prime"];

      foreach (var position in positions.Where(o => Equals(name, o.Operation.Instrument.Name)))
      {
        var side = position.Side is OrderSideEnum.Long ? OrderSideEnum.Short : OrderSideEnum.Long;
        var order = new OrderModel
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
