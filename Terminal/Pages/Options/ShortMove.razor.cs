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
  public partial class ShortMove
  {
    [Inject] IConfiguration Configuration { get; set; }

    protected virtual ControlsComponent View { get; set; }
    protected virtual ChartsComponent ChartsView { get; set; }
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
            case true when state.Previous is SubscriptionEnum.None && state.Next is SubscriptionEnum.Progress:

              CreateAccounts();
              break;

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
        State = new Map<string, StateModel>
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
      var performance = Performance.Update([account]);
      var options = await GetOptions(point, point.Time.Value);

      if (account.Orders.Count is 0 && account.Positions.Count is 0)
      {
        var orders = GetOrders(point, options);
        await adapter.SendOrders([.. orders]);
      }

      if (account.Positions.Count > 0)
      {
        var orders = GetUpdates(point, options);

        if (orders.Count > 0)
        {
          await ClosePositions(o => Equals(o.Transaction.Instrument.Derivative.Strike, orders[0].Transaction.Instrument.Derivative.Strike));
          await adapter.SendOrders(orders[1]);
        }
      }

      DealsView.UpdateItems([.. View.Adapters.Values]);
      OrdersView.UpdateItems([.. View.Adapters.Values]);
      PositionsView.UpdateItems([.. View.Adapters.Values]);
      ChartsView.UpdateItems(point.Time.Value.Ticks, "Prices", "Bars", ChartsView.GetShape<CandleShape>(point));
      PerformanceView.UpdateItems(point.Time.Value.Ticks, "Performance", "Balance", new AreaShape { Y = account.Balance });
      PerformanceView.UpdateItems(point.Time.Value.Ticks, "Performance", "PnL", PerformanceView.GetShape<LineShape>(performance.Point, SKColors.OrangeRed));
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
    /// <param name="options"></param>
    /// <returns></returns>
    protected IList<OrderModel> GetOrders(PointModel point, IList<InstrumentModel> options)
    {
      var adapter = View.Adapters["Prime"];
      var account = adapter.Account;
      var range = point.Last * 0.01;
      var shortPut = options
        ?.Where(o => o.Derivative.Side is OptionSideEnum.Put)
        ?.Where(o => o.Derivative.Strike < point.Last - 1)
        ?.LastOrDefault();

      var longPut = options
        ?.Where(o => o.Derivative.Side is OptionSideEnum.Put)
        ?.Where(o => o.Derivative.Strike < shortPut.Derivative.Strike - range)
        ?.LastOrDefault();

      var shortCall = options
        ?.Where(o => o.Derivative.Side is OptionSideEnum.Call)
        ?.Where(o => o.Derivative.Strike > point.Last + 1)
        ?.FirstOrDefault();

      var longCall = options
        ?.Where(o => o.Derivative.Side is OptionSideEnum.Call)
        ?.Where(o => o.Derivative.Strike > shortCall.Derivative.Strike + range)
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
          new OrderModel
          {
            Volume = 1,
            Side = OrderSideEnum.Long,
            Instruction = InstructionEnum.Side,
            Transaction = new() { Instrument = longPut }
          },
          new OrderModel
          {
            Volume = 1,
            Side = OrderSideEnum.Long,
            Instruction = InstructionEnum.Side,
            Transaction = new() { Instrument = longCall }
          },
          new OrderModel
          {
            Volume = 1,
            Side = OrderSideEnum.Short,
            Instruction = InstructionEnum.Side,
            Transaction = new() { Instrument = shortPut }
          },
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
    /// Update positions
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    protected IList<OrderModel> GetUpdates(PointModel point, IList<InstrumentModel> options)
    {
      var adapter = View.Adapters["Prime"];
      var account = adapter.Account;
      var openStrikes = account
        .Positions
        .Values
        .ToDictionary(o => o.Transaction.Instrument.Derivative.Strike);

      var posPut = account
        .Positions
        .Values
        .Where(o => o.Side is OrderSideEnum.Short)
        .Where(o => o.Transaction.Instrument.Derivative.Side is OptionSideEnum.Put)
        .First();

      var posCall = account
        .Positions
        .Values
        .Where(o => o.Side is OrderSideEnum.Short)
        .Where(o => o.Transaction.Instrument.Derivative.Side is OptionSideEnum.Call)
        .First();

      var order = new OrderModel
      {
        Volume = posCall.Volume,
        Side = OrderSideEnum.Short,
        Type = OrderTypeEnum.Market,
        Transaction = new()
      };

      if (point.Last + 1 > posCall.Transaction.Instrument.Derivative.Strike)
      {
        order.Transaction.Instrument = options
          .Where(o => o.Derivative.Side is OptionSideEnum.Call)
          .Where(o => o.Derivative.Strike > point.Last + 1)
          .Where(o => openStrikes.ContainsKey(o.Derivative.Strike) is false)
          .FirstOrDefault();

        return [posCall, order];
      }

      if (point.Last - 1 < posPut.Transaction.Instrument.Derivative.Strike)
      {
        order.Transaction.Instrument = options
          .Where(o => o.Derivative.Side is OptionSideEnum.Put)
          .Where(o => o.Derivative.Strike < point.Last - 1)
          .Where(o => openStrikes.ContainsKey(o.Derivative.Strike) is false)
          .LastOrDefault();

        return [posPut, order];
      }

      return [];
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
            Volume = position.Volume,
            Side = position.Side is OrderSideEnum.Long ? OrderSideEnum.Short : OrderSideEnum.Long,
            Type = OrderTypeEnum.Market,
            Transaction = new()
            {
              Instrument = position.Transaction.Instrument
            }
          };

          await adapter.SendOrders(order);
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
      var delta = order.Transaction?.Instrument?.Derivative?.Variance?.Delta;
      var side = order.Side is OrderSideEnum.Long ? 1.0 : -1.0;

      return ((delta ?? volume) * units * side) ?? 0;
    }
  }
}
