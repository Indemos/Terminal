using Canvas.Core.Models;
using Canvas.Core.Shapes;
using Distribution.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Tradier;
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
  public partial class ShortConvexProtection
  {
    [Inject] IConfiguration Configuration { get; set; }

    protected virtual ControlsComponent View { get; set; }
    protected virtual ChartsComponent ChartsView { get; set; }
    protected virtual ChartsComponent DeltaView { get; set; }
    protected virtual ChartsComponent ExposureView { get; set; }
    protected virtual ChartsComponent PerformanceView { get; set; }
    protected virtual DealsComponent DealsView { get; set; }
    protected virtual OrdersComponent OrdersView { get; set; }
    protected virtual PositionsComponent PositionsView { get; set; }
    protected virtual StatementsComponent StatementsView { get; set; }
    protected virtual PerformanceIndicator Performance { get; set; }
    protected IList<InstrumentModel> Options { get; set; } = [];
    protected IList<InstrumentModel> ProtectionOptions { get; set; } = [];
    protected InstrumentModel Instrument { get; set; } = new InstrumentModel
    {
      Name = "SPY",
      Type = InstrumentEnum.Shares,
    };

    protected IGateway Prime
    {
      get => View.Adapters["Prime"];
      set => View.Adapters["Prime"] = value;
    }

    protected static DateTime OptionDate(PointModel point)
    {
      //var date = point?.Time ?? DateTime.Now;
      var date = DateTime.Now.AddDays(4);

      switch (date.DayOfWeek)
      {
        case DayOfWeek.Sunday: date = date.AddDays(1); break;
        case DayOfWeek.Saturday: date = date.AddDays(2); break;
      }

      return date;
    }

    protected static DateTime ProtectionDate(PointModel point)
    {
      var date = OptionDate(point).AddDays(1);

      switch (date.DayOfWeek)
      {
        case DayOfWeek.Sunday: date = date.AddDays(1); break;
        case DayOfWeek.Saturday: date = date.AddDays(2); break;
      }

      return date;
    }

    protected IList<OrderModel> GetOrders(DateTime? date) => Prime
      .Account
      .Orders
      .Values.SelectMany(o => o.Orders.Append(o))
      .Where(o => Equals(o?.Instrument?.Derivative?.TradeDate?.Date, date?.Date))
      .ToList();

    protected IList<OrderModel> GetPositions(DateTime? date) => Prime
      .Account
      .Positions
      .Values.SelectMany(o => o.Orders.Append(o))
      .Where(o => o.Amount > 0)
      .Where(o => Equals(o?.Instrument?.Derivative?.TradeDate?.Date, date?.Date))
      .ToList();

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
      await DeltaView.Create("Delta");
      await ChartsView.Create("Prices");
      await ExposureView.Create("Exposure");
      await PerformanceView.Create("Performance");

      DeltaView.Composers.ForEach(o => o.ShowIndex = i => GetDateByIndex(o.Items, (int)i));
      ExposureView.Composers.ForEach(o => o.ShowIndex = i => GetDateByIndex(o.Items, (int)i));
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
        Descriptor = Configuration["Tradier:PaperAccount"],
        States = new Map<string, SummaryModel>
        {
          ["SPY"] = new SummaryModel { Instrument = Instrument, TimeFrame = TimeSpan.FromMinutes(1) }
        }
      };

      Prime = new Adapter
      {
        Account = account,
        Token = Configuration["Tradier:PaperToken"],
        SessionToken = Configuration["Tradier:Token"],
        //Source = Configuration["Simulation:Source"],
        //Speed = 1
      };

      Performance = new PerformanceIndicator { Name = "Balance" };

      // Options

      var stamp = DateTime.Now.AddDays(-1);

      Prime.Stream += async message =>
      {
        if (DateTime.Now >= stamp + TimeSpan.FromSeconds(5))
        {
          Options = await GetOptions(message.Next, OptionDate(message.Next));
          ProtectionOptions = await GetOptions(message.Next, ProtectionDate(message.Next));
          stamp = DateTime.Now;
        }
      };

      // Charts

      Prime.Stream += message =>
      {
        if (Equals(message.Next.Name, Instrument.Name))
        {
          var point = message.Next;
          var performance = Performance.Update([account]);
          var (basisDelta, optionDelta, sigma) = GetIndicators(point);
          var com = new ComponentModel { Color = SKColors.LimeGreen };
          var comUp = new ComponentModel { Color = SKColors.DeepSkyBlue };
          var comDown = new ComponentModel { Color = SKColors.OrangeRed };

          DealsView.UpdateItems([.. View.Adapters.Values]);
          OrdersView.UpdateItems([.. View.Adapters.Values]);
          PositionsView.UpdateItems([.. View.Adapters.Values]);
          ChartsView.UpdateItems(point.Time.Value.Ticks, "Prices", "Bars", ChartsView.GetShape<CandleShape>(point));
          PerformanceView.UpdateItems(point.Time.Value.Ticks, "Performance", "Balance", PerformanceView.GetShape<AreaShape>(account.Balance));
          PerformanceView.UpdateItems(point.Time.Value.Ticks, "Performance", "PnL", PerformanceView.GetShape<LineShape>(performance.Point, SKColors.OrangeRed));
          DeltaView.UpdateItems(point.Time.Value.Ticks, "Delta", "Basis Delta", new BarShape { Y = basisDelta, Component = comUp });
          DeltaView.UpdateItems(point.Time.Value.Ticks, "Delta", "Option Delta", new LineShape { Y = optionDelta, Component = comDown });
          ExposureView.UpdateItems(point.Time.Value.Ticks, "Exposure", "Sigma", new AreaShape { Y = sigma, Component = com });
        }
      };

      // Trades

      Prime.Stream += async message =>
      {
        var hasOptions = Options?.Count is not 0;
        var hasProtections = ProtectionOptions?.Count is not 0;

        if (hasOptions && hasProtections && Equals(message.Next.Name, Instrument.Name))
        {
          await OnTrade(message.Next);
        }
      };
    }

    /// <summary>
    /// Process tick data
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    protected async Task OnTrade(PointModel point)
    {
      var adapter = Prime;
      var account = adapter.Account;
      var performance = Performance.Update([account]);

      if (Options.Count is 0 || ProtectionOptions.Count is 0)
      {
        return;
      }

      if (account.Orders.Count is 0 && account.Positions.Count is 0)
      {
        var order = GetOrder(point, Options);
        await adapter.SendOrder(order);
      }

      if (GetPositions(OptionDate(point)).Count == 4)
      {
        var (protectionDelta, optionDelta, sigma) = GetIndicators(point);
        var isSell = protectionDelta < 0 && optionDelta > 0;
        var isBuy = protectionDelta > 0 && optionDelta < 0;

        if (optionDelta is 0)
        {
          await ClosePositions(o => Equals(o.Instrument.Derivative.ExpirationDate, ProtectionDate(point)));
        }
        else if (protectionDelta is 0 || isBuy || isSell)
        {
          var order = GetProtection(point, ProtectionOptions, optionDelta);

          await ClosePositions(o => Equals(o.Instrument.Derivative.ExpirationDate?.Date, ProtectionDate(point).Date));
          await adapter.SendOrder(order);
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
    /// Render indicators
    /// </summary>
    protected virtual (double, double, double) GetIndicators(PointModel point)
    {
      var adapter = Prime;
      var account = adapter.Account;
      var positions = account.Positions.Values;
      var protectionOptions = GetPositions(ProtectionDate(point));
      var options = GetPositions(OptionDate(point));
      var protectionDelta = Math.Round(protectionOptions.Sum(GetDelta), MidpointRounding.ToZero);
      var optionDelta = Math.Round(options.Sum(GetDelta), MidpointRounding.ToZero);
      var positionSigma = options.Sum(o => o.Instrument.Derivative.Volatility ?? 0);

      return (protectionDelta, optionDelta, positionSigma);
    }

    /// <summary>
    /// Get option chain
    /// </summary>
    /// <param name="point"></param>
    /// <param name="date"></param>
    /// <returns></returns>
    protected async Task<IList<InstrumentModel>> GetOptions(PointModel point, DateTime date)
    {
      var adapter = Prime;
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
    /// <param name="optionDelta"></param>
    /// <returns></returns>
    protected OrderModel GetProtection(PointModel point, IList<InstrumentModel> options, double optionDelta)
    {
      var range = point.Last * 0.01;
      var instrument = null as InstrumentModel;

      if (optionDelta < 0)
      {
        instrument = options
          ?.Where(o => o.Derivative.Side is OptionSideEnum.Put)
          ?.Where(o => o.Derivative.Strike <= point.Last)
          ?.LastOrDefault();
      }

      if (optionDelta > 0)
      {
        instrument = options
          ?.Where(o => o.Derivative.Side is OptionSideEnum.Call)
          ?.Where(o => o.Derivative.Strike >= point.Last)
          ?.FirstOrDefault();
      }

      if (instrument is null)
      {
        return null;
      }

      var order = new OrderModel
      {
        Amount = 1,
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.Market,
        Name = instrument.Name
      };

      return order;
    }

    /// <summary>
    /// Create short condor strategy
    /// </summary>
    /// <param name="point"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    protected OrderModel GetOrder(PointModel point, IList<InstrumentModel> options)
    {
      var adapter = Prime;
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
        Name = Instrument.Name,
        Type = OrderTypeEnum.Market,
        Instruction = InstructionEnum.Group,
        Orders =
        [
          new OrderModel
          {
            Amount = 1,
            Side = OrderSideEnum.Long,
            Instruction = InstructionEnum.Side,
            Name = longPut.Name
          },
          new OrderModel
          {
            Amount = 1,
            Side = OrderSideEnum.Long,
            Instruction = InstructionEnum.Side,
            Name = longCall.Name
          },
          new OrderModel
          {
            Amount = 1,
            Side = OrderSideEnum.Short,
            Instruction = InstructionEnum.Side,
            Name = shortPut.Name
          },
          new OrderModel
          {
            Amount = 1,
            Side = OrderSideEnum.Short,
            Instruction = InstructionEnum.Side,
            Name = shortCall.Name
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
      var adapter = Prime;
      var account = adapter.Account;

      foreach (var position in adapter.Account.Positions.Values.ToList())
      {
        if (condition is null || condition(position))
        {
          var order = new OrderModel
          {
            Name = position.Name,
            Amount = position.Amount,
            Type = OrderTypeEnum.Market,
            Side = position.Side is OrderSideEnum.Long ? OrderSideEnum.Short : OrderSideEnum.Long
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
      var units = order.Instrument?.Leverage;
      var delta = order.Instrument?.Derivative?.Variance?.Delta;
      var side = order.Side is OrderSideEnum.Long ? 1.0 : -1.0;

      return ((delta ?? volume) * units * side) ?? 0;
    }
  }
}
