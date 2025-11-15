using Canvas.Core.Shapes;
using Core.Conventions;
using Core.Enums;
using Core.Indicators;
using Core.Models;
using Dashboard.Components;
using Dashboard.Services;
using Lib = QuantLib;
using Simulation;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace Dashboard.Pages.Options
{
  public partial class ShortConvexProtection
  {
    ControlsComponent View { get; set; }
    ChartsComponent DataView { get; set; }
    ChartsComponent DeltaView { get; set; }
    ChartsComponent VarianceView { get; set; }
    ChartsComponent PerformanceView { get; set; }
    TransactionsComponent TransactionsView { get; set; }
    OrdersComponent OrdersView { get; set; }
    PositionsComponent PositionsView { get; set; }
    StatementsComponent StatementsView { get; set; }
    PerformanceIndicator Performance { get; set; }
    OptionPriceService PriceService { get; set; } = new(0.05, 0.05, 0.15);
    Dictionary<string, Instrument> Instruments => new()
    {
      ["SPY"] = new Instrument { Name = "SPY", TimeFrame = TimeSpan.FromMinutes(1) }
    };

    double? Price { get; set; }
    double? Strike { get; set; }


    IGateway Adapter
    {
      get => View.Adapters["Prime"];
      set => View.Adapters["Prime"] = value;
    }

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
      await DeltaView.Create("Delta");
      await VarianceView.Create("Variance");
      await PerformanceView.Create("Performance");

      DataView.Composers.ForEach(o => o.ShowIndex = i => GetDate(o.Items, (int)i));
      DeltaView.Composers.ForEach(o => o.ShowIndex = i => GetDate(o.Items, (int)i));
      VarianceView.Composers.ForEach(o => o.ShowIndex = i => GetDate(o.Items, (int)i));
      PerformanceView.Composers.ForEach(o => o.ShowIndex = i => GetDate(o.Items, (int)i));
    }

    protected override Task OnTrade()
    {
      Performance = new PerformanceIndicator();
      View.Adapters["Prime"] = new SimGateway
      {
        Messenger = Messenger,
        Connector = Connector,
        Source = "D:/Code/Options", // Configuration["Documents:Resources"],
        Account = new()
        {
          Name = "Demo",
          Balance = 25000,
          Instruments = Instruments
        }
      };

      return base.OnTrade();
    }

    protected override async Task OnViewUpdate(Instrument instrument)
    {
      Price = instrument.Price.Last;

      var adapter = Adapter;
      var price = instrument.Price;
      var account = adapter.Account;
      var positions = (await adapter.GetPositions(default)).Data;
      var performance = await Performance.Update(View.Adapters.Values);
      var (curDelta, nextDelta, sigma) = GetIndicators(positions, price);

      OrdersView.Update(View.Adapters.Values);
      PositionsView.Update(View.Adapters.Values);
      TransactionsView.Update(View.Adapters.Values);
      DataView.Update(price.Bar.Time.Value, "Data", "Bars", DataView.GetShape<CandleShape>(price));
      PerformanceView.Update(price.Time.Value, "Performance", "Balance", new AreaShape { Y = account.Balance + account.Performance });
      PerformanceView.Update(price.Time.Value, "Performance", "PnL", PerformanceView.GetShape<LineShape>(performance.Response, SKColors.OrangeRed));
      DeltaView.Update(price.Time.Value, "Delta", "Next Delta", new BarShape { Y = nextDelta, Component = ComUp });
      DeltaView.Update(price.Time.Value, "Delta", "Current Delta", new LineShape { Y = curDelta, Component = ComDown });
      VarianceView.Update(price.Time.Value, "Variance", "Sigma", new AreaShape { Y = sigma, Component = Com });
    }

    protected override async Task OnTradeUpdate(Instrument instrument)
    {
      Price = instrument.Price.Last;

      var adapter = Adapter;
      var point = instrument.Price;
      var account = adapter.Account;
      var orders = (await adapter.GetOrders(default)).Data;
      var positions = (await adapter.GetPositions(default)).Data;
      var curDate = CurDate(point);
      var nextDate = NextDate(point);
      var curPositions = Positions(positions, curDate);
      var nextPositions = Positions(positions, nextDate);

      if (orders.Count is 0 && positions.Count is 0)
      {
        Strike = Price;
        var options = await GetOptions(instrument, curDate);
        var order = GetCurOrder(options);
        await adapter.SendOrder(order);
      }

      if (orders.Count is 0 && curPositions.Count == 4)
      {
        var options = await GetOptions(instrument, nextDate);
        var (curDelta, nextDelta, sigma) = GetIndicators(positions, point);
        var isBuy = Price > Strike && nextPositions.Any(o => o.Operation.Instrument.Derivative.Side is OptionSideEnum.Put);
        var isSell = Price < Strike && nextPositions.Any(o => o.Operation.Instrument.Derivative.Side is OptionSideEnum.Call);

        if (Math.Abs((Price - Strike).Value) > 1)
        {
          //await ClosePosition(adapter, o => Equals(o?.Operation?.Instrument?.Derivative?.ExpirationDate?.Date, NextDate(point).Date));
          await ClosePosition(adapter);
          Strike = Price;
          isBuy = Price > Strike;
          isSell = Price < Strike;
        }

        if (nextPositions.Count is 0 || isBuy || isSell)
        {
          var order = GetNextOrder(options, isSell ? -1 : 1);

          await ClosePosition(adapter, o => Equals(o?.Operation?.Instrument?.Derivative?.ExpirationDate?.Date, NextDate(point).Date));
          await adapter.SendOrder(order);
        }
      }
    }

    /// <summary>
    /// Render indicators
    /// </summary>
    (double, double, double) GetIndicators(IList<Order> positions, Price point)
    {
      var nextPositions = Positions(positions, NextDate(point));
      var curPositions = Positions(positions, CurDate(point)).Where(o => o.Side is OrderSideEnum.Short);
      var sigma = curPositions.Sum(o => o.Operation.Instrument.Derivative.Volatility ?? 0);
      var customCurDelta = Math.Round(curPositions.Sum(o =>
      {
        var instrument = o.Operation.Instrument;

        return PriceService.Delta(
          instrument.Derivative.Side is OptionSideEnum.Put ? Lib.Option.Type.Put : Lib.Option.Type.Call,
          Price,
          instrument.Derivative.Strike,
          0.01) * -1 * 100.0;

      }), MidpointRounding.ToZero);

      var customNextDelta = Math.Round(nextPositions.Sum(o =>
      {
        var instrument = o.Operation.Instrument;

        return PriceService.Delta(
          instrument.Derivative.Side is OptionSideEnum.Put ? Lib.Option.Type.Put : Lib.Option.Type.Call,
          Price,
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
    Order GetNextOrder(IList<Instrument> options, int direction)
    {
      var instrument = null as Instrument;

      if (direction < 0)
      {
        instrument = options
          ?.Where(o => o.Derivative.Side is OptionSideEnum.Put)
          ?.Where(o => o.Derivative.Strike <= Price)
          ?.LastOrDefault();
      }

      if (direction > 0)
      {
        instrument = options
          ?.Where(o => o.Derivative.Side is OptionSideEnum.Call)
          ?.Where(o => o.Derivative.Strike >= Price)
          ?.FirstOrDefault();
      }

      if (instrument is null)
      {
        return null;
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
  }
}
