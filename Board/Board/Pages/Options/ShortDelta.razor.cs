using Board.Components;
using Canvas.Core.Models;
using Canvas.Core.Shapes;
using Core.Conventions;
using Core.Enums;
using Core.Extensions;
using Core.Indicators;
using Core.Models;
using Core.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Orleans;
using Simulation;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Board.Pages.Options
{
  public partial class ShortDelta
  {
    [Inject] IClusterClient Connector { get; set; }
    [Inject] IConfiguration Configuration { get; set; }
    [Inject] MessageService Messenger { get; set; }
    [Inject] StateService State { get; set; }

    ControlsComponent View { get; set; }
    ChartsComponent DataView { get; set; }
    ChartsComponent IndicatorsView { get; set; }
    ChartsComponent PerformanceView { get; set; }
    TransactionsComponent TransactionsView { get; set; }
    OrdersComponent OrdersView { get; set; }
    PositionsComponent PositionsView { get; set; }
    StatementsComponent StatementsView { get; set; }
    PerformanceIndicator Performance { get; set; }

    Dictionary<string, InstrumentModel> instruments = new()
    {
      ["SPY"] = new InstrumentModel { Name = "SPY", TimeFrame = TimeSpan.FromMinutes(1) }
    };

    IGateway Adapter
    {
      get => View.Adapters.Get(string.Empty);
      set => View.Adapters[string.Empty] = value;
    }

    /// <summary>
    /// Setup views and adapters
    /// </summary>
    /// <param name="setup"></param>
    protected override async Task OnAfterRenderAsync(bool setup)
    {
      await DataView.Create("Prices");
      await IndicatorsView.Create("Indicators");
      await PerformanceView.Create("Performance");

      DataView.Composers.ForEach(o => o.ShowIndex = i => GetDateByIndex(o.Items, (int)i));
      IndicatorsView.Composers.ForEach(o => o.ShowIndex = i => GetDateByIndex(o.Items, (int)i));
      PerformanceView.Composers.ForEach(o => o.ShowIndex = i => GetDateByIndex(o.Items, (int)i));

      if (setup)
      {
        Messenger.Subscribe<PriceModel>(Update);
        State.Subscribe(state =>
        {
          if (state.Previous is SubscriptionEnum.None && state.Next is SubscriptionEnum.Progress)
          {
            Performance = new PerformanceIndicator();
            Adapter = new SimGateway
            {
              Messenger = Messenger,
              Connector = Connector,
              Source = Configuration["Documents:Resources"],
              Account = new AccountModel
              {
                Name = "Demo",
                Balance = 25000,
                Instruments = instruments
              }
            };
          }

          return Task.CompletedTask;
        });
      }

      await base.OnAfterRenderAsync(setup);
    }

    /// <summary>
    /// Time axis renderer
    /// </summary>
    /// <param name="items"></param>
    /// <param name="index"></param>
    string GetDateByIndex(IList<IShape> items, int index)
    {
      var empty = index <= 0 ? items.FirstOrDefault()?.X : items.LastOrDefault()?.X;
      var stamp = (long)(items.ElementAtOrDefault(index)?.X ?? empty ?? DateTime.Now.Ticks);

      return $"{new DateTime(stamp):HH:mm}";
    }

    /// <summary>
    /// Process tick data
    /// </summary>
    /// <param name="price"></param>
    async Task Update(PriceModel price)
    {
      var account = Adapter.Account;
      var performance = await Performance.Update(View.Adapters.Values);
      var options = await GetOptions(price, new DateTime(price.Time.Value));
      var orders = await Adapter.GetOrders(default);
      var positions = await Adapter.GetPositions(default);

      if (orders.Count is 0 && positions.Count is 0)
      {
        var order = GetOrder(price, options);
        if (order is not null) await Adapter.SendOrder(order);
      }

      if (positions.Count is not 0)
      {
        var (basisDelta, optionDelta) = UpdateIndicators(price, positions);
        var order = GetUpdate(price, basisDelta, optionDelta);
        if (order is not null) await Adapter.SendOrder(order);
      }

      OrdersView.Update(View.Adapters.Values);
      PositionsView.Update(View.Adapters.Values);
      TransactionsView.Update(View.Adapters.Values);
      DataView.Update(price.Time.Value, "Prices", "Bars", DataView.GetShape<CandleShape>(price));
      PerformanceView.Update(price.Time.Value, "Performance", "Balance", new AreaShape { Y = account.Balance });
      PerformanceView.Update(price.Time.Value, "Performance", "PnL", PerformanceView.GetShape<LineShape>(performance.Response, SKColors.OrangeRed));
    }

    /// <summary>
    /// Render indicators
    /// </summary>
    (double, double) UpdateIndicators(PriceModel point, IList<OrderModel> positions)
    {
      var adapter = View.Adapters["Prime"];
      var account = adapter.Account;
      var comUp = new ComponentModel { Color = SKColors.DeepSkyBlue };
      var comDown = new ComponentModel { Color = SKColors.OrangeRed };

      var basisDelta = Math.Round(positions
        .Where(o => o.Operation.Instrument.Derivative is null)
        .Sum(GetDelta), MidpointRounding.ToZero);

      var optionDelta = Math.Round(positions
        .Where(o => o.Operation.Instrument.Derivative is not null)
        .Sum(GetDelta), MidpointRounding.ToZero);

      IndicatorsView.Update(point.Time.Value, "Indicators", "Stock Delta", new AreaShape { Y = basisDelta, Component = comUp });
      IndicatorsView.Update(point.Time.Value, "Indicators", "Option Delta", new AreaShape { Y = optionDelta, Component = comDown });

      return (basisDelta, optionDelta);
    }

    /// <summary>
    /// Hedge each delta change with shares
    /// </summary>
    /// <param name="price"></param>
    /// <param name="basisDelta"></param>
    /// <param name="optionDelta"></param>
    OrderModel GetUpdate(PriceModel price, double basisDelta, double optionDelta)
    {
      var delta = optionDelta + basisDelta;

      if (Equals(optionDelta, -basisDelta) is false)
      {
        var order = new OrderModel
        {
          Amount = Math.Abs(delta),
          Type = OrderTypeEnum.Market,
          Side = delta < 0 ? OrderSideEnum.Long : OrderSideEnum.Short,
          Operation = new() { Instrument = instruments.Get("SPY") }
        };

        return order;
      }

      return null;
    }

    /// <summary>
    /// Get option chain
    /// </summary>
    /// <param name="price"></param>
    /// <param name="date"></param>
    async Task<IList<InstrumentModel>> GetOptions(PriceModel price, DateTime date)
    {
      var screener = new CriteriaModel
      {
        MinDate = date,
        MaxDate = date,
        Instrument = instruments.Get("SPY"),
      };

      return await Adapter.GetOptions(screener);
    }

    /// <summary>
    /// Create short condor strategy
    /// </summary>
    /// <param name="point"></param>
    /// <param name="options"></param>
    OrderModel GetOrder(PriceModel point, IList<InstrumentModel> options)
    {
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
            Operation = new() { Instrument = longPut }
          },
          new OrderModel
          {
            Amount = 1,
            Side = OrderSideEnum.Long,
            Operation = new() { Instrument = longCall }
          },
          new OrderModel
          {
            Amount = 1,
            Side = OrderSideEnum.Short,
            Operation = new() { Instrument = shortPut }
          },
          new OrderModel
          {
            Amount = 1,
            Side = OrderSideEnum.Short,
            Operation = new() { Instrument = shortCall }
          }
        ]
      };

      return order;
    }

    /// <summary>
    /// Get position delta
    /// </summary>
    /// <param name="order"></param>
    static double GetDelta(OrderModel order)
    {
      var volume = order.Operation.Amount;
      var units = order.Operation?.Instrument?.Leverage;
      var delta = order.Operation?.Instrument?.Derivative?.Variance?.Delta;
      var side = order.Side is OrderSideEnum.Long ? 1.0 : -1.0;

      return ((delta ?? volume) * units * side) ?? 0;
    }
  }
}
