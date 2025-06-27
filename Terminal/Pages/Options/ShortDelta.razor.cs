using Canvas.Core.Models;
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
  public partial class ShortDelta
  {
    [Inject] IConfiguration Configuration { get; set; }

    protected virtual ControlsComponent View { get; set; }
    protected virtual ChartsComponent ChartsView { get; set; }
    protected virtual ChartsComponent IndicatorsView { get; set; }
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
    /// <returns></returns>
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
      await IndicatorsView.Create("Indicators");
      await PerformanceView.Create("Performance");

      ChartsView.Composers.ForEach(o => o.ShowIndex = i => GetDateByIndex(o.Items, (int)i));
      IndicatorsView.Composers.ForEach(o => o.ShowIndex = i => GetDateByIndex(o.Items, (int)i));
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
        State = new Map<string, SummaryModel>
        {
          ["SPY"] = new SummaryModel { Instrument = new InstrumentModel { Name = "SPY", TimeFrame = TimeSpan.FromMinutes(1) } },
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
        .ForEach(adapter => adapter.Stream += async message => await OnData(message.Next));
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
        var (basisDelta, optionDelta) = UpdateIndicators(point);
        var orders = GetUpdates(point, basisDelta, optionDelta);
        await adapter.SendOrders([.. orders]);
      }

      DealsView.UpdateItems([.. View.Adapters.Values]);
      OrdersView.UpdateItems([.. View.Adapters.Values]);
      PositionsView.UpdateItems([.. View.Adapters.Values]);
      ChartsView.UpdateItems(point.Time.Value.Ticks, "Prices", "Bars", ChartsView.GetShape<CandleShape>(point));
      PerformanceView.UpdateItems(point.Time.Value.Ticks, "Performance", "Balance", new AreaShape { Y = account.Balance });
      PerformanceView.UpdateItems(point.Time.Value.Ticks, "Performance", "PnL", PerformanceView.GetShape<LineShape>(performance.Point, SKColors.OrangeRed));
    }

    /// <summary>
    /// Render indicators
    /// </summary>
    protected virtual (double, double) UpdateIndicators(PointModel point)
    {
      var adapter = View.Adapters["Prime"];
      var account = adapter.Account;
      var positions = account.Positions.Values;
      var comUp = new ComponentModel { Color = SKColors.DeepSkyBlue };
      var comDown = new ComponentModel { Color = SKColors.OrangeRed };

      var basisDelta = Math.Round(account
        .Positions
        .Values
        .Where(o => o.Instrument.Derivative is null)
        .Sum(GetDelta), MidpointRounding.ToZero);

      var optionDelta = Math.Round(account
        .Positions
        .Values
        .Where(o => o.Instrument.Derivative is not null)
        .Sum(GetDelta), MidpointRounding.ToZero);

      IndicatorsView.UpdateItems(point.Time.Value.Ticks, "Indicators", "Stock Delta", new AreaShape { Y = basisDelta, Component = comUp });
      IndicatorsView.UpdateItems(point.Time.Value.Ticks, "Indicators", "Option Delta", new AreaShape { Y = optionDelta, Component = comDown });

      return (basisDelta, optionDelta);
    }

    /// <summary>
    /// Hedge each delta change with shares
    /// </summary>
    /// <param name="point"></param>
    /// <param name="basisDelta"></param>
    /// <param name="optionDelta"></param>
    /// <returns></returns>
    public IList<OrderModel> GetUpdates(PointModel point, double basisDelta, double optionDelta)
    {
      var delta = optionDelta + basisDelta;

      if (Math.Abs(delta) > 0)
      {
        var order = new OrderModel
        {
          Amount = Math.Abs(delta),
          Type = OrderTypeEnum.Market,
          Side = delta < 0 ? OrderSideEnum.Long : OrderSideEnum.Short,
          Instrument = point.Instrument
        };

        return [order];
      }

      return [];
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
        ?.Where(o => o.Derivative.Strike <= point.Last)
        ?.LastOrDefault();

      var longPut = options
        ?.Where(o => o.Derivative.Side is OptionSideEnum.Put)
        ?.Where(o => o.Derivative.Strike < shortPut.Derivative.Strike - range)
        ?.LastOrDefault();

      var shortCall = options
        ?.Where(o => o.Derivative.Side is OptionSideEnum.Call)
        ?.Where(o => o.Derivative.Strike >= point.Last)
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
            Amount = 1,
            Side = OrderSideEnum.Long,
            Instruction = InstructionEnum.Side,
            Instrument = longPut
          },
          new OrderModel
          {
            Amount = 1,
            Side = OrderSideEnum.Long,
            Instruction = InstructionEnum.Side,
            Instrument = longCall
          },
          new OrderModel
          {
            Amount = 1,
            Side = OrderSideEnum.Short,
            Instruction = InstructionEnum.Side,
            Instrument = shortPut
          },
          new OrderModel
          {
            Amount = 1,
            Side = OrderSideEnum.Short,
            Instruction = InstructionEnum.Side,
            Instrument = shortCall          }
        ]
      };

      return [order];
    }

    /// <summary>
    /// Get position delta
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    protected static double GetDelta(OrderModel order)
    {
      var volume = order.Amount;
      var units = order.Instrument?.Leverage;
      var delta = order.Instrument?.Derivative?.Variance?.Delta;
      var side = order.Side is OrderSideEnum.Long ? 1.0 : -1.0;

      return ((delta ?? volume) * units * side) ?? 0;
    }
  }
}
