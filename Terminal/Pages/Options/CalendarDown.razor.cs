using Canvas.Core.Shapes;
using Distribution.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Simulation;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Components;
using Terminal.Core.Collections;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Indicators;
using Terminal.Core.Models;
using Terminal.Services;

namespace Terminal.Pages.Options
{
  public partial class CalendarDown
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
        States = new Map<string, SummaryModel>
        {
          ["SPY"] = new SummaryModel { TimeFrame = TimeSpan.FromMinutes(1), Instrument = new InstrumentModel { Name = "SPY" } }
        }
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
        .ForEach(adapter => adapter.Stream += async message => await OnData(message));
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
      var performance = Performance.Update([account]);
      var longOptions = await GetOptions(point, point.Time.Value.AddDays(14));
      var shortOptions = await GetOptions(point, point.Time.Value);

      if (account.Orders.Count is 0 && account.Positions.Count is 0)
      {
        var order = GetOrder(point, longOptions, shortOptions);
        if (order is not null) await adapter.SendOrder(order);
      }

      if (account.Positions.Count > 0)
      {
        await UpdateOptions(point, longOptions, shortOptions);
        await UpdateShares(point);
      }

      DealsView.UpdateItems([.. View.Adapters.Values]);
      OrdersView.UpdateItems([.. View.Adapters.Values]);
      PositionsView.UpdateItems([.. View.Adapters.Values]);
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
      var screener = new ConditionModel
      {
        MinDate = date,
        MaxDate = date,
        Instrument = point.Instrument
      };

      return (await adapter.GetOptions(screener)).Data;
    }

    /// <summary>
    /// Create short condor strategy
    /// </summary>
    /// <param name="point"></param>
    /// <param name="longOptions"></param>
    /// <param name="shortOptions"></param>
    /// <returns></returns>
    protected OrderModel GetOrder(PointModel point, IList<InstrumentModel> longOptions, IList<InstrumentModel> shortOptions)
    {
      var adapter = View.Adapters["Prime"];
      var account = adapter.Account;
      var range = point.Last * 0.005;
      var longPut = longOptions
        ?.Where(o => o.Derivative.Side is OptionSideEnum.Put)
        ?.Where(o => o.Derivative.Strike > point.Last + 1)
        ?.FirstOrDefault();

      var shortPut = shortOptions
        ?.Where(o => o.Derivative.Side is OptionSideEnum.Put)
        ?.Where(o => o.Derivative.Strike < longPut.Derivative.Strike - range)
        ?.LastOrDefault();

      if (shortPut is null || longPut is null)
      {
        return null;
      }

      var order = new OrderModel
      {
        Type = OrderTypeEnum.Market,
        Instruction = InstructionEnum.Group,
        Orders =
        [
          new OrderModel
          {
            Amount = 1,
            Side = OrderSideEnum.Long,
            Instruction = InstructionEnum.Side,
            Transaction = new() { Instrument = longPut }
          },
          new OrderModel
          {
            Amount = 1,
            Side = OrderSideEnum.Short,
            Instruction = InstructionEnum.Side,
            Transaction = new() { Instrument = shortPut }
          },
        ]
      };

      return order;
    }

    /// <summary>
    /// Reopen short position for more premium
    /// </summary>
    /// <param name="point"></param>
    /// <param name="longOptions"></param>
    /// <param name="shortOptions"></param>
    /// <returns></returns>
    protected async Task UpdateOptions(PointModel point, IList<InstrumentModel> longOptions, IList<InstrumentModel> shortOptions)
    {
      var adapter = View.Adapters["Prime"];
      var account = adapter.Account;
      var shortPosition = account
        .Positions
        .Values
        .Where(o => o.Side is OrderSideEnum.Short)
        .Where(o => o.Transaction.Instrument.Derivative is not null)
        .FirstOrDefault();

      var isExpired = shortPosition is not null && shortPosition.GetClosePrice() <= 0.05;
      var isPenetrated = shortPosition is not null && point.Last + 1 > shortPosition.Transaction.Instrument.Derivative.Strike;

      if (isExpired || isPenetrated)
      {
        await ClosePositions(o => o.Transaction.Instrument.Derivative is not null && o.Side is OrderSideEnum.Short);
      }

      var shortUpdate = account
        .Positions
        .Values
        .Where(o => o.Side is OrderSideEnum.Short)
        .Where(o => o.Transaction.Instrument.Derivative is not null)
        .ToList();

      if (shortUpdate.Count is 0)
      {
        var order = GetOrder(point, longOptions, shortOptions);
        var shortOrder = order?.Orders?.LastOrDefault();

        if (shortOrder is null || shortOrder.Transaction.Instrument.Point.Last <= 0.10)
        {
          return;
        }

        shortOrder.Type = OrderTypeEnum.Market;
        await adapter.SendOrder(shortOrder);
      }
    }


    /// <summary>
    /// Hedge drawdowns with shares
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    protected async Task UpdateShares(PointModel point)
    {
      var adapter = View.Adapters["Prime"];
      var account = adapter.Account;
      var sharesPosition = account
        .Positions
        .Values
        .Where(o => o.Transaction.Instrument.Derivative is null)
        .ToList();

      var strike = account
        .Positions
        .Values
        .Where(o => o.Side is OrderSideEnum.Long)
        .Where(o => o.Transaction.Instrument.Derivative is not null)
        .First()
        .Transaction
        .Instrument
        .Derivative
        .Strike;

      if (sharesPosition.Count is not 0 && point.Last.Value + 1 < strike)
      {
        await ClosePositions(o => o.Transaction.Instrument.Derivative is null);
      }

      if (sharesPosition.Count is 0 && point.Last.Value + 1 > strike)
      {
        var order = new OrderModel
        {
          Amount = 100,
          Type = OrderTypeEnum.Market,
          Side = OrderSideEnum.Long,
          Transaction = new() { Instrument = point.Instrument }
        };

        await adapter.SendOrder(order);
      }
    }

    /// <summary>
    /// Close positions
    /// </summary>
    /// <param name="condition"></param>
    /// <returns></returns>
    public virtual async Task ClosePositions(Func<OrderModel, bool> condition = null)
    {
      var adapter = View.Adapters["Prime"];
      var account = adapter.Account;

      foreach (var position in adapter.Account.Positions.Values.ToList())
      {
        if (condition is null || condition(position))
        {
          var order = new OrderModel
          {
            Amount = position.Amount,
            Side = position.Side is OrderSideEnum.Long ? OrderSideEnum.Short : OrderSideEnum.Long,
            Type = OrderTypeEnum.Market,
            Transaction = new()
            {
              Instrument = position.Transaction.Instrument
            }
          };

          await adapter.SendOrder(order);
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
      var volume = order.Amount;
      var units = order.Transaction?.Instrument?.Leverage;
      var delta = order.Transaction?.Instrument?.Derivative?.Variance?.Delta;
      var side = order.Side is OrderSideEnum.Long ? 1.0 : -1.0;

      return ((delta ?? volume) * units * side) ?? 0;
    }
  }
}
