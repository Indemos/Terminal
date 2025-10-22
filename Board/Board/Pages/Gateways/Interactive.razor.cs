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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Board.Pages.Gateways
{
  public partial class Interactive
  {
    [Inject] IClusterClient Connector { get; set; }
    [Inject] IConfiguration Configuration { get; set; }
    [Inject] StreamService Streamer { get; set; }
    [Inject] SubscriptionService Observer { get; set; }

    protected ControlsComponent View { get; set; }
    protected ChartsComponent ChartsView { get; set; }
    protected ChartsComponent PerformanceView { get; set; }
    protected TransactionsComponent TransactionsView { get; set; }
    protected OrdersComponent OrdersView { get; set; }
    protected PositionsComponent PositionsView { get; set; }
    protected StatementsComponent StatementsView { get; set; }
    protected PerformanceIndicator Performance { get; set; }
    protected InstrumentModel Instrument { get; set; } = new InstrumentModel
    {
      Name = "ESZ5",
      Exchange = "CME",
      Type = InstrumentEnum.Futures,
      TimeFrame = TimeSpan.FromMinutes(1),
      Basis = new InstrumentModel { Name = "ES" }
    };

    protected override async Task OnAfterRenderAsync(bool setup)
    {
      if (setup)
      {
        await ChartsView.Create("Prices");
        await PerformanceView.Create("Performance");

        ChartsView.Composers.ForEach(o => o.ShowIndex = i => GetDateByIndex(o.Items, (int)i));
        PerformanceView.Composers.ForEach(o => o.ShowIndex = i => GetDateByIndex(o.Items, (int)i));

        Observer.OnState += state =>
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

    /// <summary>
    /// Time axis renderer
    /// </summary>
    /// <param name="items"></param>
    /// <param name="index"></param>
    /// <returns></returns>
    protected string GetDateByIndex(IList<IShape> items, int index)
    {
      var empty = index <= 0 ? items.FirstOrDefault()?.X : items.LastOrDefault()?.X;
      var stamp = (long)(items.ElementAtOrDefault(index)?.X ?? empty ?? DateTime.Now.Ticks);

      return $"{new DateTime(stamp):HH:mm}";
    }

    protected void CreateAccounts()
    {
      Performance = new PerformanceIndicator { Name = "Balance" };

      var adapter = View.Adapters["Prime"] = new InteractiveBrokers.Gateway
      {
        Streamer = Streamer,
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

    protected async Task OnPrice(PriceModel price)
    {
      var name = Instrument.Name;
      var adapter = View.Adapters["Prime"];
      var account = adapter.Account;
      var instrument = Instrument;
      var performance = await Performance.Update([adapter]);
      var orders = await adapter.GetOrders(default);
      var positions = await adapter.GetPositions(default);

      if (orders.IsEmpty() && positions.IsEmpty())
      {
        OpenPositions(Instrument, price, 1);
        Done(async () =>
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

      ChartsView.UpdateItems(price.Bar.Time.Value, "Prices", "Bars", ChartsView.GetShape<CandleShape>(price));
      PerformanceView.UpdateItems(price.Bar.Time.Value, "Performance", "Balance", new AreaShape { Y = account.Balance });
      PerformanceView.UpdateItems(price.Bar.Time.Value, "Performance", "PnL", new LineShape { Y = performance.Response.Last });
      TransactionsView.UpdateItems([.. View.Adapters.Values]);
      OrdersView.UpdateItems([.. View.Adapters.Values]);
      PositionsView.UpdateItems([.. View.Adapters.Values]);
    }

    protected async void OpenPositions(InstrumentModel instrument, PriceModel price, double direction)
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
        Price = price.Last + 15 * direction,
        Operation = new() { Instrument = instrument }
      };

      var SL = new OrderModel
      {
        Amount = 1,
        Side = stopSide,
        Type = OrderTypeEnum.Stop,
        Instruction = InstructionEnum.Brace,
        Price = price.Last - 15 * direction,
        Operation = new() { Instrument = instrument }
      };

      var order = new OrderModel
      {
        Amount = 1,
        Side = side,
        Type = OrderTypeEnum.Market,
        Operation = new() { Instrument = instrument },
        Orders = [SL, TP]
      };

      await adapter.SendOrder(order);
    }

    protected async Task ClosePositions(string name)
    {
      var adapter = View.Adapters["Prime"];
      var positions = await adapter.GetPositions(default);

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
    protected static async void Done(Action action, int interval)
    {
      await Task.Delay(interval);
      action();
    }
  }
}
