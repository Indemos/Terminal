using Canvas.Core.Shapes;
using Distribution.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Components;
using Terminal.Core.Collections;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Indicators;
using Terminal.Core.Models;
using Terminal.Services;
using Tradier;

namespace Terminal.Pages.Gateways
{
  public partial class TradierDemo
  {
    [Inject] IConfiguration Configuration { get; set; }

    protected ControlsComponent View { get; set; }
    protected virtual ChartsComponent ChartsView { get; set; }
    protected virtual ChartsComponent PerformanceView { get; set; }
    protected virtual DealsComponent DealsView { get; set; }
    protected virtual OrdersComponent OrdersView { get; set; }
    protected virtual PositionsComponent PositionsView { get; set; }
    protected virtual StatementsComponent StatementsView { get; set; }
    protected PerformanceIndicator Performance { get; set; }
    protected InstrumentModel Instrument { get; set; } = new InstrumentModel
    {
      Name = "SPY",
      Type = InstrumentEnum.Shares,
    };

    protected override async Task OnAfterRenderAsync(bool setup)
    {
      if (setup)
      {
        await ChartsView.Create("Prices");
        await PerformanceView.Create("Performance");

        InstanceService<SubscriptionService>.Instance.Update += state =>
        {
          switch (true)
          {
            case true when state.Previous is SubscriptionEnum.None && state.Next is SubscriptionEnum.Progress: CreateAccounts(); break;
            case true when state.Previous is SubscriptionEnum.Progress && state.Next is SubscriptionEnum.Stream:

              var account = View.Adapters["Prime"].Account;

              DealsView.UpdateItems([.. View.Adapters.Values]);
              OrdersView.UpdateItems([.. View.Adapters.Values]);
              PositionsView.UpdateItems([.. View.Adapters.Values]);

              break;
          }
        };
      }

      await base.OnAfterRenderAsync(setup);
    }

    /// <summary>
    /// Account setup
    /// </summary>
    protected virtual void CreateAccounts()
    {
      var account = new Account
      {
        Descriptor = Configuration["Tradier:PaperAccount"],
        States = new Map<string, SummaryModel>
        {
          [Instrument.Name] = new SummaryModel { Instrument = Instrument, TimeFrame = TimeSpan.FromMinutes(1) }
        }
      };

      View.Adapters["Prime"] = new Adapter
      {
        Account = account,
        Token = Configuration["Tradier:PaperToken"],
        SessionToken = Configuration["Tradier:Token"]
      };

      Performance = new PerformanceIndicator { Name = "Balance" };

      View
        .Adapters
        .Values
        .ForEach(adapter => adapter.Stream += async message =>
        {
          if (Equals(message.Instrument.Name, Instrument.Name))
          {
            await OnData(message);
          }
        });
    }

    protected async Task OnData(PointModel point)
    {
      var name = Instrument.Name;
      var account = View.Adapters["Prime"].Account;
      var instrument = account.States[Instrument.Name].Instrument;
      var performance = Performance.Update([account]);
      var openOrders = account.Orders.Values.Where(o => Equals(o.Name, name));
      var openPositions = account.Positions.Values.Where(o => Equals(o.Name, name));

      if (openOrders.IsEmpty() && openPositions.IsEmpty())
      {
        await OpenPositions(Instrument, 1);
        await Done(async () =>
        {
          var position = account
            .Positions
            .Values
            .Where(o => Equals(o.Name, name))
            .FirstOrDefault();

          if (position is not null)
          {
            await ClosePositions(name);
          }

        }, 10000);
      }

      ChartsView.UpdateItems(point.Time.Value.Ticks, "Prices", "Bars", ChartsView.GetShape<CandleShape>(point));
      PerformanceView.UpdateItems(point.Time.Value.Ticks, "Performance", "Balance", new AreaShape { Y = account.Balance });
      PerformanceView.UpdateItems(point.Time.Value.Ticks, "Performance", "PnL", new LineShape { Y = performance.Point.Last });
      DealsView.UpdateItems([.. View.Adapters.Values]);
      OrdersView.UpdateItems([.. View.Adapters.Values]);
      PositionsView.UpdateItems([.. View.Adapters.Values]);
    }

    protected double? GetPrice(double direction) => direction > 0 ?
      Instrument.Point.Ask :
      Instrument.Point.Bid;

    protected async Task OpenPositions(InstrumentModel instrument, double direction)
    {
      var adapter = View.Adapters["Prime"];
      var side = direction > 0 ? OrderSideEnum.Long : OrderSideEnum.Short;
      var stopSide = direction < 0 ? OrderSideEnum.Long : OrderSideEnum.Short;

      var TP = new OrderModel
      {
        Amount = 10,
        Side = stopSide,
        Type = OrderTypeEnum.Limit,
        Instruction = InstructionEnum.Brace,
        Price = GetPrice(direction) + 15 * direction,
        Transaction = new() { Instrument = instrument }
      };

      var SL = new OrderModel
      {
        Amount = 10,
        Side = stopSide,
        Type = OrderTypeEnum.Stop,
        Instruction = InstructionEnum.Brace,
        Price = GetPrice(-direction) - 15 * direction,
        Transaction = new() { Instrument = instrument }
      };

      var order = new OrderModel
      {
        Side = side,
        Amount = 10,
        Price = GetPrice(direction),
        Type = OrderTypeEnum.Market,
        Transaction = new() { Instrument = instrument },
        Orders = [SL, TP]
      };

      await adapter.SendOrder(order);
    }

    protected async Task ClosePositions(string name)
    {
      var adapter = View.Adapters["Prime"];

      foreach (var position in adapter.Account.Positions.Values.Where(o => Equals(name, o.Name)))
      {
        var side = position.Side is OrderSideEnum.Long ? OrderSideEnum.Short : OrderSideEnum.Long;
        var order = new OrderModel
        {
          Side = side,
          Amount = position.Amount,
          Type = OrderTypeEnum.Market,
          Transaction = new()
          {
            Instrument = position.Transaction.Instrument
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
