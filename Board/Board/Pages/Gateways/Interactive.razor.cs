using Board.Components;
using Canvas.Core.Shapes;
using Core.Enums;
using Core.Indicators;
using Core.Models;
using Core.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Orleans;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Board.Pages.Gateways
{
  public partial class Interactive
  {
    [Inject] IClusterClient Connector { get; set; }
    [Inject] IConfiguration Configuration { get; set; }
    [Inject] CommunicationService Messenger { get; set; }

    protected ControlsComponent View { get; set; }
    protected virtual ChartsComponent ChartsView { get; set; }
    protected virtual ChartsComponent PerformanceView { get; set; }
    protected virtual TransactionsComponent TransactionsView { get; set; }
    protected virtual OrdersComponent OrdersView { get; set; }
    protected virtual PositionsComponent PositionsView { get; set; }
    protected virtual StatementsComponent StatementsView { get; set; }
    protected PerformanceIndicator Performance { get; set; }
    protected virtual InstrumentModel Instrument { get; set; } = new InstrumentModel
    {
      Name = "ESH5",
      Exchange = "CME",
      Type = InstrumentEnum.Futures,
      Basis = new InstrumentModel { Name = "ES" }
    };

    protected override async Task OnAfterRenderAsync(bool setup)
    {
      if (setup)
      {
        await ChartsView.Create("Prices");
        await PerformanceView.Create("Performance");

        Messenger.OnMessage += state =>
        {
          switch (true)
          {
            case true when state.Previous is SubscriptionEnum.None && state.Next is SubscriptionEnum.Progress: CreateAccounts(); break;
            case true when state.Previous is SubscriptionEnum.Progress && state.Next is SubscriptionEnum.Stream:

              var account = View.Adapters["Prime"].Account;

              TransactionsView.UpdateItems([.. View.Adapters.Values]);
              OrdersView.UpdateItems([.. View.Adapters.Values]);
              PositionsView.UpdateItems([.. View.Adapters.Values]);

              break;
          }
        };
      }

      await base.OnAfterRenderAsync(setup);
    }

    protected virtual void CreateAccounts()
    {
      Performance = new PerformanceIndicator { Name = "Balance" };

      var adapter = View.Adapters["Prime"] = new InteractiveBrokers.Gateway
      {
        Connector = Connector,
        Port = int.Parse(Configuration["InteractiveBrokers:PaperPort"]),
        Account = new AccountModel
        {
          Name = Configuration["InteractiveBrokers:PaperAccount"],
          Instruments = new()
          {
            [Instrument.Name] = Instrument
          }
        }
      };

      adapter.Subscription += OnPrice;
    }

    public virtual async Task OnPrice(PriceModel price)
    {
      var name = Instrument.Name;
      var adapter = View.Adapters["Prime"];
      var account = adapter.Account;
      var instrument = Instrument;
      var performance = await Performance.Update([adapter]);
      var orders = await adapter.Orders(default);
      var positions = await adapter.Positions(default);

      if (orders.IsEmpty() && positions.IsEmpty())
      {
        await OpenPositions(Instrument, 1);
        await Done(async () =>
        {
          var position = positions
            .Where(o => Equals(o.Operation.Instrument.Name, name))
            .FirstOrDefault();

          if (position is not null)
          {
            await ClosePositions(position.Operation.Instrument.Name);
          }

        }, 10000);
      }

      ChartsView.UpdateItems(price.Time.Value, "Prices", "Bars", ChartsView.GetShape<CandleShape>(price));
      PerformanceView.UpdateItems(price.Time.Value, "Performance", "Balance", new AreaShape { Y = account.Balance });
      PerformanceView.UpdateItems(price.Time.Value, "Performance", "PnL", new LineShape { Y = performance.Response.Last });
      TransactionsView.UpdateItems([.. View.Adapters.Values]);
      OrdersView.UpdateItems([.. View.Adapters.Values]);
      PositionsView.UpdateItems([.. View.Adapters.Values]);
    }

    protected double? GetPrice(double direction) => direction > 0 ?
      Instrument.Price.Ask :
      Instrument.Price.Bid;

    protected async Task OpenPositions(InstrumentModel instrument, double direction)
    {
      var adapter = View.Adapters["Prime"];
      var side = direction > 0 ? OrderSideEnum.Long : OrderSideEnum.Short;
      var stopSide = direction < 0 ? OrderSideEnum.Long : OrderSideEnum.Short;

      var TP = new OrderModel
      {
        Amount = 1,
        Side = stopSide,
        Type = OrderTypeEnum.Limit,
        Instruction = InstructionEnum.Brace,
        Price = GetPrice(direction) + 15 * direction,
        Operation = new() { Instrument = instrument }
      };

      var SL = new OrderModel
      {
        Amount = 1,
        Side = stopSide,
        Type = OrderTypeEnum.Stop,
        Instruction = InstructionEnum.Brace,
        Price = GetPrice(-direction) - 15 * direction,
        Operation = new() { Instrument = instrument }
      };

      var order = new OrderModel
      {
        Amount = 1,
        Side = side,
        Price = GetPrice(direction),
        Type = OrderTypeEnum.Market,
        Operation = new() { Instrument = instrument },
        Orders = [SL, TP]
      };

      await adapter.SendOrder(order);
    }

    protected async Task ClosePositions(string name)
    {
      var adapter = View.Adapters["Prime"];
      var positions = await adapter.Positions(default);

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
