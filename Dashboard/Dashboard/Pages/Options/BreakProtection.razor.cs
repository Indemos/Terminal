using Canvas.Core.Shapes;
using Core.Enums;
using Core.Indicators;
using Core.Models;
using Dashboard.Components;
using Dashboard.Services;
using Simulation;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Lib = QuantLib;

namespace Dashboard.Pages.Options
{
  public partial class BreakProtection
  {
    static double IV { get; set; } = 0.15;
    static double DivRate { get; set; } = 0.05;
    static double RiskRate { get; set; } = 0.05;
    double? Strike { get; set; }
    double? StrikeUp { get; set; }
    double? StrikeDown { get; set; }
    OptionSideEnum? Side { get; set; }

    ControlsComponent View { get; set; }
    ChartsComponent DataView { get; set; }
    ChartsComponent PerformanceView { get; set; }
    TransactionsComponent TransactionsView { get; set; }
    OrdersComponent OrdersView { get; set; }
    PositionsComponent PositionsView { get; set; }
    StatementsComponent StatementsView { get; set; }
    PerformanceIndicator Performance { get; set; }
    OptionPriceService PriceService { get; set; } = new(RiskRate, DivRate, IV);
    Dictionary<string, Instrument> Instruments => new()
    {
      ["SPY"] = new Instrument { Name = "SPY", TimeFrame = TimeSpan.FromMinutes(1) }
    };

    DateTime CurDate(Price point)
    {
      var date = new DateTime(point.Time.Value);

      switch (date.DayOfWeek)
      {
        case DayOfWeek.Sunday: date = date.AddDays(1); break;
        case DayOfWeek.Saturday: date = date.AddDays(2); break;
      }

      return date;
    }

    DateTime NextDate(Price point)
    {
      var date = CurDate(point).AddDays(1);

      switch (date.DayOfWeek)
      {
        case DayOfWeek.Sunday: date = date.AddDays(1); break;
        case DayOfWeek.Saturday: date = date.AddDays(2); break;
      }

      return date;
    }

    IList<Order> Positions(IList<Order> positions, DateTime? date) => [.. positions
      .SelectMany(o => o.Orders.Append(o))
      .Where(o => o.Amount > 0)
      .Where(o => Equals(o?.Operation?.Instrument?.Derivative?.TradeDate?.Date, date?.Date))];

    protected override async Task OnView()
    {
      await DataView.Create("Data");
      await PerformanceView.Create("Performance");

      DataView.Composers.ForEach(o => o.ShowIndex = i => GetDate(o.Items, (int)i));
      PerformanceView.Composers.ForEach(o => o.ShowIndex = i => GetDate(o.Items, (int)i));
    }

    protected override Task OnTrade()
    {
      Performance = new PerformanceIndicator();
      Adapter = new SimGateway
      {
        Connector = Connector,
        Source = Configuration["Documents:Resources"],
        Account = new()
        {
          Descriptor = "Demo",
          Balance = 25000,
          Instruments = Instruments
        }
      };

      return base.OnTrade();
    }

    protected override async void OnViewUpdate(Instrument instrument)
    {
      var adapter = Adapter;
      var price = instrument.Price;
      var account = adapter.Account;
      var positions = (await adapter.GetPositions(default)).Data;
      var performance = await Performance.Update(Adapters.Values);

      OrdersView.Update(Adapters.Values);
      PositionsView.Update(Adapters.Values);
      TransactionsView.Update(Adapters.Values);
      DataView.Update(price.Bar.Time.Value, "Data", "Bars", DataView.GetShape<CandleShape>(price));
      PerformanceView.Update(price.Time.Value, "Performance", "Balance", new AreaShape { Y = account.Balance + account.Performance });
      PerformanceView.Update(price.Time.Value, "Performance", "PnL", PerformanceView.GetShape<LineShape>(performance.Response, SKColors.OrangeRed));
    }

    protected override async Task OnTradeUpdate(Instrument instrument)
    {
      var adapter = Adapter;
      var point = instrument.Price;
      var price = point.Last;
      var account = adapter.Account;
      var orders = (await adapter.GetOrders(default)).Data;
      var positions = (await adapter.GetPositions(default)).Data;
      var curDate = CurDate(point);
      var nextDate = NextDate(point);
      var curPositions = Positions(positions, curDate);
      var nextPositions = Positions(positions, nextDate);

      if (orders.Count is 0 && positions.Count is 0)
      {
        Strike = StrikeUp = StrikeDown = price;
        var options = await GetOptions(instrument, curDate);
        var order = GetCurOrder(options);
        await adapter.SendOrder(order);
      }

      if (orders.Count is 0 && curPositions.Count == 4)
      {
        var options = await GetOptions(instrument, nextDate);
        var isBuy = price > Strike && Side is OptionSideEnum.Put;
        var isSell = price < Strike && Side is OptionSideEnum.Call;
        var range = Range(price, IV);
        var contracts = nextPositions.Where(o => o.Side is OrderSideEnum.Long).Sum(o => o.Operation.Amount) / 2;
        var step = contracts is 0 ? 0.1 : Math.Max(0.1, (range / contracts).Value);
        
        if (nextPositions.Count is 0 || isBuy || isSell)
        {
          var order = GetNextOrder(options, isSell ? -1 : 1, nextPositions);

          if (order is not null)
          {
            await adapter.SendOrder(order);
            Side = isSell ? OptionSideEnum.Put : OptionSideEnum.Call;
          }
        }

        if (price - StrikeUp > step)
        {
          var order = GetCoverOrder(options, 1, price, nextPositions);

          if (order is not null)
          {
            await adapter.SendOrder(order);
            StrikeUp = price;
          }
        }

        if (StrikeDown - price > step)
        {
          var order = GetCoverOrder(options, -1, price, nextPositions);

          if (order is not null)
          {
            await adapter.SendOrder(order);
            StrikeDown = price;
          }
        }
      }
    }

    /// <summary>
    /// Render indicators
    /// </summary>
    (double, double, double) GetIndicators(IList<Order> positions, Price point)
    {
      var price = point.Last;
      var nextPositions = Positions(positions, NextDate(point));
      var curPositions = Positions(positions, CurDate(point)).Where(o => o.Side is OrderSideEnum.Short);
      var sigma = curPositions.Sum(o => o.Operation.Instrument.Derivative.Volatility ?? 0);
      var customCurDelta = Math.Round(curPositions.Sum(o =>
      {
        var instrument = o.Operation.Instrument;

        return PriceService.Delta(
          instrument.Derivative.Side is OptionSideEnum.Put ? Lib.Option.Type.Put : Lib.Option.Type.Call,
          price,
          instrument.Derivative.Strike,
          0.01) * -1 * 100.0;

      }), MidpointRounding.ToZero);

      var customNextDelta = Math.Round(nextPositions.Sum(o =>
      {
        var instrument = o.Operation.Instrument;

        return PriceService.Delta(
          instrument.Derivative.Side is OptionSideEnum.Put ? Lib.Option.Type.Put : Lib.Option.Type.Call,
          price,
          instrument.Derivative.Strike,
          0.01) * 100.0;

      }), MidpointRounding.ToZero);

      return (customCurDelta, customNextDelta, sigma);
    }

    /// <summary>
    /// Get option chain
    /// </summary>
    /// <param name="instrument"></param>
    /// <param name="date"></param>
    async Task<IList<Instrument>> GetOptions(Instrument instrument, DateTime date)
    {
      var screener = new Criteria
      {
        MinDate = date,
        MaxDate = date,
        Instrument = instrument
      };

      return (await Adapter.GetOptions(screener)).Data;
    }

    /// <summary>
    /// Create short condor strategy
    /// </summary>
    /// <param name="options"></param>
    /// <param name="direction"></param>
    Order GetNextOrder(IList<Instrument> options, int direction, IList<Order> positions)
    {
      var countLongs = positions
        .Where(o => o.Side is OrderSideEnum.Long)
        .Where(o => o.Operation.Instrument.Derivative.Side is OptionSideEnum.Call)
        .Sum(o => o.Operation.Amount);

      var countShorts = positions
        .Where(o => o.Side is OrderSideEnum.Long)
        .Where(o => o.Operation.Instrument.Derivative.Side is OptionSideEnum.Put)
        .Sum(o => o.Operation.Amount);

      var noLong = direction > 0 && countLongs > countShorts;
      var noShort = direction < 0 && countLongs < countShorts;

      if (noLong || noShort)
      {
        return null;
      }

      var instrument = null as Instrument;

      if (direction < 0)
      {
        instrument = options
          ?.Where(o => o.Derivative.Side is OptionSideEnum.Put)
          ?.Where(o => o.Derivative.Strike > Strike)
          ?.FirstOrDefault();
      }

      if (direction > 0)
      {
        instrument = options
          ?.Where(o => o.Derivative.Side is OptionSideEnum.Call)
          ?.Where(o => o.Derivative.Strike < Strike)
          ?.LastOrDefault();
      }

      var order = new Order
      {
        Amount = 1,
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.Market,
        Operation = new Operation { Instrument = instrument }
      };

      return order;
    }

    /// <summary>
    /// Create short condor strategy
    /// </summary>
    /// <param name="options"></param>
    /// <param name="direction"></param>
    Order GetCoverOrder(IList<Instrument> options, int direction, double? price, IList<Order> positions)
    {
      var instrument = null as Instrument;
      var countLongs = positions
        .Where(o => o.Side is OrderSideEnum.Long)
        .Where(o => direction < 0 ?
          o.Operation.Instrument.Derivative.Side is OptionSideEnum.Put :
          o.Operation.Instrument.Derivative.Side is OptionSideEnum.Call)
        .Sum(o => o.Operation.Amount);

      var countShorts = positions
        .Where(o => o.Side is OrderSideEnum.Short)
        .Where(o => direction < 0 ?
          o.Operation.Instrument.Derivative.Side is OptionSideEnum.Put :
          o.Operation.Instrument.Derivative.Side is OptionSideEnum.Call)
        .Sum(o => o.Operation.Amount);

      if (countLongs - countShorts <= 1)
      {
        return null;
      }

      if (direction < 0)
      {
        instrument = options
          ?.Where(o => o.Derivative.Side is OptionSideEnum.Put)
          ?.Where(o => o.Derivative.Strike <= price)
          ?.LastOrDefault();
      }

      if (direction > 0)
      {
        instrument = options
          ?.Where(o => o.Derivative.Side is OptionSideEnum.Call)
          ?.Where(o => o.Derivative.Strike >= price)
          ?.FirstOrDefault();
      }

      var order = new Order
      {
        Side = OrderSideEnum.Short,
        Type = OrderTypeEnum.Market,
        Amount = 1, // longAmount - shortAmount,
        Operation = new Operation { Instrument = instrument }
      };

      return order;
    }

    /// <summary>
    /// Create short condor strategy
    /// </summary>
    /// <param name="options"></param>
    Order GetCurOrder(IList<Instrument> options)
    {
      var range = Strike * 0.01;
      var shortPut = options
        ?.Where(o => o.Derivative.Side is OptionSideEnum.Put)
        ?.Where(o => o.Derivative.Strike <= Strike)
        ?.LastOrDefault();

      var longPut = options
        ?.Where(o => o.Derivative.Side is OptionSideEnum.Put)
        ?.Where(o => o.Derivative.Strike < shortPut.Derivative.Strike - range)
        ?.LastOrDefault();

      var shortCall = options
        ?.Where(o => o.Derivative.Side is OptionSideEnum.Call)
        ?.Where(o => o.Derivative.Strike >= Strike)
        ?.FirstOrDefault();

      var longCall = options
        ?.Where(o => o.Derivative.Side is OptionSideEnum.Call)
        ?.Where(o => o.Derivative.Strike > shortCall.Derivative.Strike + range)
        ?.FirstOrDefault();

      if (shortPut is null || shortCall is null || longPut is null || longCall is null)
      {
        return null;
      }

      var order = new Order
      {
        Orders =
        [
          new Order
          {
            Amount = 1,
            Descriptor = "Condor",
            Side = OrderSideEnum.Long,
            Operation = new Operation { Instrument = longPut }
          },
          new Order
          {
            Amount = 1,
            Descriptor = "Condor",
            Side = OrderSideEnum.Long,
            Operation = new Operation { Instrument = longCall }
          },
          new Order
          {
            Amount = 1,
            Descriptor = "Condor",
            Side = OrderSideEnum.Short,
            Operation = new Operation { Instrument = shortPut }
          },
          new Order
          {
            Amount = 1,
            Descriptor = "Condor",
            Side = OrderSideEnum.Short,
            Operation = new Operation { Instrument = shortCall }
          }
        ]
      };

      return order;
    }

    /// <summary>
    /// Expected daily variance 
    /// </summary>
    /// <param name="price"></param>
    /// <param name="volatility"></param>
    double? Range(double? price, double? volatility) => Math.Sqrt(1.0 / 252.0) * price * volatility;
  }
}
