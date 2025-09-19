using Canvas.Core.Models;
using Canvas.Core.Shapes;
using Distribution.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using QuantLib;
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
using Terminal.Core.Extensions;
using Terminal.Core.Indicators;
using Terminal.Core.Models;
using Terminal.Services;

namespace Terminal.Pages.Options
{
  public partial class ShortProtection
  {
    [Inject] IConfiguration Configuration { get; set; }

    protected virtual double? Strike { get; set; }
    protected virtual ControlsComponent View { get; set; }
    protected virtual ChartsComponent ChartsView { get; set; }
    protected virtual ChartsComponent IndicatorsView { get; set; }
    protected virtual ChartsComponent DeltaView { get; set; }
    protected virtual ChartsComponent PerformanceView { get; set; }
    protected virtual DealsComponent DealsView { get; set; }
    protected virtual OrdersComponent OrdersView { get; set; }
    protected virtual PositionsComponent PositionsView { get; set; }
    protected virtual StatementsComponent StatementsView { get; set; }
    protected virtual PerformanceIndicator Performance { get; set; }
    protected virtual MaIndicator HPF { get; set; }
    protected virtual MaIndicator LPF { get; set; }
    protected virtual OptionPriceService OptionService { get; set; }

    /// <summary>
    /// Setup views and adapters
    /// </summary>
    /// <param name="setup"></param>
    protected override async Task OnAfterRenderAsync(bool setup)
    {
      await CreateViews();

      if (setup)
      {
        OptionService = new OptionPriceService(0.05, 0.05, 0.10);

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
      await DeltaView.Create("Delta");
      await ChartsView.Create("Prices");
      await IndicatorsView.Create("Indicators");
      await PerformanceView.Create("Performance");

      DeltaView.Composers.ForEach(o => o.ShowIndex = i => GetDateByIndex(o.Items, (int)i));
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

      HPF = new MaIndicator { Name = "HPF", Interval = 5 };
      LPF = new MaIndicator { Name = "LPF", Interval = 50 };
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
      var options = await GetOptions(point, point.Time.Value);
      var averageL = LPF.Update(account.States.Get(point.Instrument.Name).PointGroups);
      var averageH = HPF.Update(account.States.Get(point.Instrument.Name).PointGroups);
      var (basisDelta, callDelta, putDelta, indicator) = UpdateIndicators(point);

      if (account.Orders.Count is 0 && account.Positions.Count is 0)
      {
        Strike = point.Last;
        var order = GetOrder(point, options);
        await adapter.SendOrder(order);
      }

      if (account.Positions.Count > 0)
      {
        //await GammaHedge(point, averageL, averageH, callDelta, putDelta, basisDelta, indicator);
        //await StrikeHedge(point);
        await AverageHedge(point, averageL, averageH);
        //await SideDeltaHedge(point);
        //await SideHedge(point, callDelta, putDelta);
        //await BarCountHedge(point);
      }

      DealsView.UpdateItems([.. View.Adapters.Values]);
      OrdersView.UpdateItems([.. View.Adapters.Values]);
      PositionsView.UpdateItems([.. View.Adapters.Values]);
      ChartsView.UpdateItems(point.Time.Value.Ticks, "Prices", "Bars", ChartsView.GetShape<CandleShape>(point));
      ChartsView.UpdateItems(point.Time.Value.Ticks, "Prices", "LPF", new LineShape { Y = averageL.Point.Last });
      ChartsView.UpdateItems(point.Time.Value.Ticks, "Prices", "HPF", new LineShape { Y = averageH.Point.Last });
      PerformanceView.UpdateItems(point.Time.Value.Ticks, "Performance", "Balance", new AreaShape { Y = account.Balance });
      PerformanceView.UpdateItems(point.Time.Value.Ticks, "Performance", "PnL", PerformanceView.GetShape<LineShape>(performance.Point, SKColors.OrangeRed));
    }

    /// <summary>
    /// Calculate delta of the entire condor and open protective position in the same direction
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    protected virtual async Task GammaHedge(
      PointModel point,
      MaIndicator averageL,
      MaIndicator averageH,
      double callDelta,
      double putDelta,
      double basisDelta,
      double indicator)
    {
      var adapter = View.Adapters["Prime"];
      var account = adapter.Account;
      var optionDelta = putDelta + callDelta;
      var isLong = basisDelta < 0 && indicator > 0;
      var isShort = basisDelta > 0 && indicator < 0;

      if (basisDelta is 0 || isLong || isShort)
      {
        var order = new OrderModel
        {
          Amount = 50,
          Type = OrderTypeEnum.Market,
          Side = isShort ? OrderSideEnum.Short : OrderSideEnum.Long,
          Transaction = new() { Instrument = point.Instrument }
        };

        await ClosePositions(o => o.Transaction.Instrument.Derivative is null);
        await adapter.SendOrder(order);
      }
    }

    /// <summary>
    /// Open protective position with the same delta as a single short option when it becomes ITM and its delta increases
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    protected virtual async Task SideDeltaHedge(PointModel point)
    {
      var adapter = View.Adapters["Prime"];
      var account = adapter.Account;
      var shortCall = account
        .Positions
        .Values
        .Where(o => o.Side is OrderSideEnum.Short)
        .Where(o => o.Transaction.Instrument.Derivative?.Side is OptionSideEnum.Call)
        .FirstOrDefault();

      var shortPut = account
        .Positions
        .Values
        .Where(o => o.Side is OrderSideEnum.Short)
        .Where(o => o.Transaction.Instrument.Derivative?.Side is OptionSideEnum.Put)
        .FirstOrDefault();

      if (shortPut is not null && shortCall is not null)
      {
        var rawDelta = GetDelta(point.Last < shortPut.Transaction.Instrument.Derivative.Strike ? shortPut : shortCall);
        var optionDelta = Math.Round(rawDelta, MidpointRounding.ToZero);
        var basisDelta = Math.Round(account
          .Positions
          .Values
          .Where(o => o.Transaction.Instrument.Derivative is null)
          .Sum(GetDelta), MidpointRounding.ToZero);

        var delta = optionDelta + basisDelta;

        if (Equals(optionDelta, -basisDelta) is false)
        {
          var order = new OrderModel
          {
            Amount = Math.Abs(delta),
            Type = OrderTypeEnum.Market,
            Side = delta < 0 ? OrderSideEnum.Long : OrderSideEnum.Short,
            Transaction = new() { Instrument = point.Instrument }
          };

          await adapter.SendOrder(order);
        }
      }
    }

    /// <summary>
    /// Protect only one side of the condor, either call or put spread, based on which delta is penetrated deeper
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    protected virtual async Task SideHedge(PointModel point, double callDelta, double putDelta)
    {
      var adapter = View.Adapters["Prime"];
      var account = adapter.Account;
      var position = account.Positions.Values.FirstOrDefault(o => o.Transaction.Instrument.Derivative is null);
      var isLong = (position is null || position.Side is OrderSideEnum.Short) && Math.Abs(putDelta) < Math.Abs(callDelta);
      var isShort = (position is null || position.Side is OrderSideEnum.Long) && Math.Abs(putDelta) > Math.Abs(callDelta);

      if (isLong || isShort)
      {
        var order = new OrderModel
        {
          Amount = 50,
          Type = OrderTypeEnum.Market,
          Side = isShort ? OrderSideEnum.Short : OrderSideEnum.Long,
          Transaction = new() { Instrument = point.Instrument }
        };

        await ClosePositions(o => o.Transaction.Instrument.Derivative is null);
        await adapter.SendOrder(order);
      }
    }

    /// <summary>
    /// Open position in the direction of prevailing bars, above or below moving average
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    protected virtual async Task BarCountHedge(PointModel point)
    {
      var adapter = View.Adapters["Prime"];
      var account = adapter.Account;
      var summary = account.States.Get(point.Instrument.Name);
      var position = account.Positions.Values.FirstOrDefault(o => o.Transaction.Instrument.Derivative is null);
      var countUps = summary.PointGroups.Sum(o => o.Series.Get("HPF").Last > o.Series.Get("LPF").Last ? 1 : 0);
      var countDowns = summary.PointGroups.Sum(o => o.Series.Get("HPF").Last < o.Series.Get("LPF").Last ? 1 : 0);
      var isLong = (position is null || position.Side is OrderSideEnum.Short) && countUps > countDowns;
      var isShort = (position is null || position.Side is OrderSideEnum.Long) && countUps < countDowns;
      var com = new ComponentModel { Color = SKColors.LimeGreen };
      var comUp = new ComponentModel { Color = SKColors.DeepSkyBlue };
      var comDown = new ComponentModel { Color = SKColors.OrangeRed };

      if (isLong || isShort)
      {
        var order = new OrderModel
        {
          Amount = 50,
          Type = OrderTypeEnum.Market,
          Side = isShort ? OrderSideEnum.Short : OrderSideEnum.Long,
          Transaction = new() { Instrument = point.Instrument }
        };

        await ClosePositions(o => o.Transaction.Instrument.Derivative is null);
        await adapter.SendOrder(order);
      }

      IndicatorsView.UpdateItems(point.Time.Value.Ticks, "Indicators", "Ups", new BarShape { Y = countUps, Component = comUp });
      IndicatorsView.UpdateItems(point.Time.Value.Ticks, "Indicators", "Downs", new BarShape { Y = -countDowns, Component = comDown });
    }

    /// <summary>
    /// Hedge total option position 
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    protected virtual async Task StrikeHedge(PointModel point)
    {
      var adapter = View.Adapters["Prime"];
      var account = adapter.Account;
      var position = account.Positions.Values.FirstOrDefault(o => o.Transaction.Instrument.Derivative is null);
      var isUp = (position is null || position.Side is OrderSideEnum.Short) && point.Last > Strike;
      var isDown = (position is null || position.Side is OrderSideEnum.Long) && point.Last < Strike;

      if (isUp || isDown)
      {
        var order = new OrderModel
        {
          Amount = 50,
          Type = OrderTypeEnum.Market,
          Side = isUp ? OrderSideEnum.Long : OrderSideEnum.Short,
          Transaction = new() { Instrument = point.Instrument }
        };

        await ClosePositions(o => o.Transaction.Instrument.Derivative is null);
        await adapter.SendOrder(order);
      }
    }

    /// <summary>
    /// Hedge total option position based on moving average
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    protected virtual async Task AverageHedge(PointModel point, MaIndicator averageL, MaIndicator averageH)
    {
      var adapter = View.Adapters["Prime"];
      var account = adapter.Account;
      var position = account.Positions.Values.FirstOrDefault(o => o.Transaction.Instrument.Derivative is null);
      var isUp = (position is null || position.Side is OrderSideEnum.Short) && averageH.Point.Last > averageL.Point.Last;
      var isDown = (position is null || position.Side is OrderSideEnum.Long) && averageH.Point.Last < averageL.Point.Last;

      if (isUp || isDown)
      {
        var order = new OrderModel
        {
          Amount = 50,
          Type = OrderTypeEnum.Market,
          Side = isUp ? OrderSideEnum.Long : OrderSideEnum.Short,
          Transaction = new() { Instrument = point.Instrument }
        };

        await ClosePositions(o => o.Transaction.Instrument.Derivative is null);
        await adapter.SendOrder(order);
      }
    }

    /// <summary>
    /// Render indicators
    /// </summary>
    protected virtual (double, double, double, double) UpdateIndicators(PointModel point)
    {
      var adapter = View.Adapters["Prime"];
      var account = adapter.Account;
      var positions = account.Positions.Values;
      var com = new ComponentModel { Color = SKColors.LimeGreen };
      var comX = new ComponentModel { Color = SKColors.Gray };
      var comUp = new ComponentModel { Color = SKColors.DeepSkyBlue };
      var comDown = new ComponentModel { Color = SKColors.OrangeRed };

      var basisDelta = Math.Round(account
        .Positions
        .Values
        .Where(o => o.Transaction.Instrument.Derivative is null)
        .Sum(GetDelta), MidpointRounding.ToZero);

      var putDelta = Math.Round(account
        .Positions
        .Values
        .Where(o => o.Transaction.Instrument.Derivative?.Side is OptionSideEnum.Put)
        .Sum(GetDelta), MidpointRounding.ToZero);

      var callDelta = Math.Round(account
        .Positions
        .Values
        .Where(o => o.Transaction.Instrument.Derivative?.Side is OptionSideEnum.Call)
        .Sum(GetDelta), MidpointRounding.ToZero);

      var customOptionDelta = Math.Round(account
        .Positions
        .Values
        .Where(o => o.Transaction.Instrument.Derivative is not null)
        .Sum(o =>
        {
          var instrument = o.Transaction.Instrument;
          var delta = OptionService.ComputeDelta(
            instrument.Derivative.Side is OptionSideEnum.Put ? Option.Type.Put : Option.Type.Call,
            instrument.Basis.Point.Last,
            instrument.Derivative.Strike,
            0.01) * o.GetSide() * 100.0;

          return delta.Value;

        }), MidpointRounding.ToZero);

      var indicator = account
        .Positions
        .Values
        .Where(o => o.Transaction.Instrument.Derivative is not null)
        .Sum(o =>
        {
          var side = o.Side is OrderSideEnum.Short ? -1 : 1;
          var optionSide = o.Transaction.Instrument.Derivative.Side is OptionSideEnum.Put ? -1 : 1;

          return o.Transaction.Instrument.Derivative.Variance.Gamma.Value * side * optionSide;

        }) * 100;

      DeltaView.UpdateItems(point.Time.Value.Ticks, "Delta", "Basis Delta", new BarShape { Y = basisDelta, Component = com });
      DeltaView.UpdateItems(point.Time.Value.Ticks, "Delta", "Option Delta", new LineShape { Y = callDelta + putDelta, Component = com });
      DeltaView.UpdateItems(point.Time.Value.Ticks, "Delta", "Indicator", new LineShape { Y = indicator, Component = comX });

      return (basisDelta, callDelta, putDelta, indicator);
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
    protected OrderModel GetOrder(PointModel point, IList<InstrumentModel> options)
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
            Side = OrderSideEnum.Long,
            Instruction = InstructionEnum.Side,
            Transaction = new() { Instrument = longCall }
          },
          new OrderModel
          {
            Amount = 1,
            Side = OrderSideEnum.Short,
            Instruction = InstructionEnum.Side,
            Transaction = new() { Instrument = shortPut }
          },
          new OrderModel
          {
            Amount = 1,
            Side = OrderSideEnum.Short,
            Instruction = InstructionEnum.Side,
            Transaction = new() { Instrument = shortCall }
          }
        ]
      };

      return order;
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
            Amount = position.Transaction.Amount,
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
