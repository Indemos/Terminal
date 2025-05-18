using Canvas.Core.Shapes;
using Distribution.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Simulation;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Terminal.Components;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Indicators;
using Terminal.Core.Models;
using Terminal.Services;

namespace Terminal.Pages.Options
{
  public partial class LongCalendar
  {
    [Inject] IConfiguration Configuration { get; set; }

    protected virtual ControlsComponent View { get; set; }
    protected virtual ChartsComponent ChartsView { get; set; }
    protected virtual EstimatesComponent EstimatesView { get; set; }
    protected virtual ChartsComponent PerformanceView { get; set; }
    protected virtual DealsComponent DealsView { get; set; }
    protected virtual OrdersComponent OrdersView { get; set; }
    protected virtual PositionsComponent PositionsView { get; set; }
    protected virtual StatementsComponent StatementsView { get; set; }
    protected virtual PerformanceIndicator Performance { get; set; }

    /// <summary>
    /// Setup views and adapters
    /// </summary>
    /// <param name="setup"></param>
    protected override async Task OnAfterRenderAsync(bool setup)
    {
      await CreateViews();

      if (setup)
      {
        InstanceService<SubscriptionService>.Instance.OnUpdate += state =>
        {
          switch (true)
          {
            case true when state.Previous is SubscriptionEnum.None && state.Next is SubscriptionEnum.Progress:

              CreateAccounts();
              break;

            case true when state.Previous is SubscriptionEnum.Progress && state.Next is SubscriptionEnum.Pause:

              StatementsView.UpdateItems(View.Adapters.Values.Select(o => o.Account));
              break;

            case true when state.Previous is SubscriptionEnum.Progress && state.Next is SubscriptionEnum.Stream:

              var account = View.Adapters["Prime"].Account;

              DealsView.UpdateItems(account.Deals);
              OrdersView.UpdateItems(account.Orders.Values);
              PositionsView.UpdateItems(account.Positions.Values);

              break;
          }
        };
      }

      await base.OnAfterRenderAsync(setup);
    }

    /// <summary>
    /// Views
    /// </summary>
    protected virtual async Task CreateViews()
    {
      await ChartsView.Create("Prices");
      await PerformanceView.Create("Performance");
      await EstimatesView.Create("Estimates");

      ChartsView.Composers.ForEach(o => o.ShowIndex = i => GetDateByIndex(o.Items, (int)i));
      PerformanceView.Composers.ForEach(o => o.ShowIndex = i => GetDateByIndex(o.Items, (int)i));
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

    /// <summary>
    /// Accounts
    /// </summary>
    protected virtual void CreateAccounts()
    {
      var account = new Account
      {
        Balance = 25000,
        Summary = new ConcurrentDictionary<string, StateModel>
        {
          ["SPY"] = new StateModel { Instrument = new InstrumentModel { Name = "SPY", TimeFrame = TimeSpan.FromMinutes(1) } },
        },
      };

      View.Adapters["Prime"] = new Adapter
      {
        Speed = 1,
        Account = account,
        Source = Configuration["Simulation:Source"]
      };

      Performance = new PerformanceIndicator { Name = "Balance" };

      View
        .Adapters
        .Values
        .ForEach(adapter => adapter.DataStream += async message => await OnData(message.Next));
    }

    /// <summary>
    /// Process tick data
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    protected async Task OnData(PointModel point)
    {
      var adapter = View.Adapters["Prime"];
      var account = adapter.Account;
      var performance = Performance.Calculate([account]);
      var longOptions = await GetOptions(point, point.Time.Value.AddDays(14));
      var shortOptions = await GetOptions(point, point.Time.Value);

      if (account.Orders.Count is 0 && account.Positions.Count is 0)
      {
        var orders = GetOrders(point, longOptions, shortOptions);
        await adapter.CreateOrders([.. orders]);
      }

      if (account.Positions.Count > 0)
      {
        var sharesPosition = account
          .Positions
          .Values
          .Where(o => o.Transaction.Instrument.Derivative is null)
          .ToList();

        var strike = account
          .Positions
          .Values
          .First(o => o.Side is OrderSideEnum.Long)
          .Transaction
          .Instrument
          .Derivative
          .Strike;

        if (sharesPosition.Count is not 0 && point.Last.Value > strike)
        {
          await ClosePositions(InstrumentEnum.Shares);
        }

        if (sharesPosition.Count is 0 && point.Last.Value < strike)
        {
          var order = new OrderModel
          {
            Volume = 50,
            Type = OrderTypeEnum.Market,
            Side = OrderSideEnum.Short,
            Transaction = new() { Instrument = point.Instrument }
          };

          await adapter.CreateOrders([order]);
        }
      }

      DealsView.UpdateItems(account.Deals);
      OrdersView.UpdateItems(account.Orders.Values);
      PositionsView.UpdateItems(account.Positions.Values);
      ChartsView.UpdateItems(point.Time.Value.Ticks, "Prices", "Bars", ChartsView.GetShape<CandleShape>(point));
      PerformanceView.UpdateItems(point.Time.Value.Ticks, "Performance", "Balance", new AreaShape { Y = account.Balance });
      PerformanceView.UpdateItems(point.Time.Value.Ticks, "Performance", "PnL", PerformanceView.GetShape<LineShape>(performance.Point, SKColors.OrangeRed));
      EstimatesView.UpdateItems(adapter, point, account.Positions.Values);
    }

    /// <summary>
    /// Get option chain
    /// </summary>
    /// <param name="point"></param>
    /// <param name="date"></param>
    /// <returns></returns>
    protected async Task<IList<InstrumentModel>> GetOptions(PointModel point, DateTime date)
    {
      var adapter = View.Adapters["Prime"];
      var account = adapter.Account;
      var screener = new InstrumentScreenerModel
      {
        MinDate = date,
        MaxDate = date,
        Instrument = point.Instrument
      };

      return (await adapter.GetOptions(screener, [])).Data;
    }

    /// <summary>
    /// Create short condor strategy
    /// </summary>
    /// <param name="point"></param>
    /// <param name="longOptions"></param>
    /// <param name="shortOptions"></param>
    /// <returns></returns>
    protected IList<OrderModel> GetOrders(PointModel point, IList<InstrumentModel> longOptions, IList<InstrumentModel> shortOptions)
    {
      var adapter = View.Adapters["Prime"];
      var account = adapter.Account;
      var range = point.Last * 0.005;
      var longPut = longOptions
        ?.Where(o => o.Derivative.Side is OptionSideEnum.Put)
        ?.Where(o => o.Derivative.Strike > point.Last)
        ?.FirstOrDefault();

      var longCall = longOptions
        ?.Where(o => o.Derivative.Side is OptionSideEnum.Call)
        ?.Where(o => o.Derivative.Strike < point.Last)
        ?.LastOrDefault();

      var shortPut = shortOptions
        ?.Where(o => o.Derivative.Side is OptionSideEnum.Put)
        ?.Where(o => o.Derivative.Strike < longPut.Derivative.Strike - range)
        ?.LastOrDefault();

      var shortCall = shortOptions
        ?.Where(o => o.Derivative.Side is OptionSideEnum.Call)
        ?.Where(o => o.Derivative.Strike > longCall.Derivative.Strike + range)
        ?.FirstOrDefault();

      if (shortPut is null || shortCall is null || longPut is null || longCall is null)
      {
        return [];
      }

      var order = new OrderModel
      {
        Type = OrderTypeEnum.Market,
        Instruction = InstructionEnum.Group,
        Orders =
        [
          //new OrderModel
          //{
          //  Volume = 1,
          //  Side = OrderSideEnum.Long,
          //  Instruction = InstructionEnum.Side,
          //  Transaction = new() { Instrument = longPut }
          //},
          new OrderModel
          {
            Volume = 1,
            Side = OrderSideEnum.Long,
            Instruction = InstructionEnum.Side,
            Transaction = new() { Instrument = longCall }
          },
          //new OrderModel
          //{
          //  Volume = 1,
          //  Side = OrderSideEnum.Short,
          //  Instruction = InstructionEnum.Side,
          //  Transaction = new() { Instrument = shortPut }
          //},
          new OrderModel
          {
            Volume = 1,
            Side = OrderSideEnum.Short,
            Instruction = InstructionEnum.Side,
            Transaction = new() { Instrument = shortCall }
          }
        ]
      };

      return [order];
    }

    /// <summary>
    /// Close positions
    /// </summary>
    /// <param name="instrumentType"></param>
    /// <returns></returns>
    public virtual async Task ClosePositions(InstrumentEnum? instrumentType = null)
    {
      var adapter = View.Adapters["Prime"];
      var account = adapter.Account;

      foreach (var position in adapter.Account.Positions.Values.ToList())
      {
        var insEmpty = instrumentType is null;
        var insShares = instrumentType is InstrumentEnum.Shares && position.Transaction.Instrument.Derivative is null;
        var insOptions = instrumentType is InstrumentEnum.Options && position.Transaction.Instrument.Derivative is not null;

        if (insEmpty || insShares || insOptions)
        {
          var order = new OrderModel
          {
            Volume = position.Volume,
            Side = position.Side is OrderSideEnum.Long ? OrderSideEnum.Short : OrderSideEnum.Long,
            Type = OrderTypeEnum.Market,
            Transaction = new()
            {
              Instrument = position.Transaction.Instrument
            }
          };

          await adapter.CreateOrders(order);
        }
      }
    }

    /// <summary>
    /// Get position delta
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    protected static double GetDelta(OrderModel order)
    {
      var volume = order.Volume;
      var units = order.Transaction?.Instrument?.Leverage;
      var delta = order.Transaction?.Instrument?.Derivative?.Exposure?.Delta;
      var side = order.Side is OrderSideEnum.Long ? 1.0 : -1.0;

      return ((delta ?? volume) * units * side) ?? 0;
    }
  }
}
