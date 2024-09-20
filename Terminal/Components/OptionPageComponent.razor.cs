using Canvas.Core.Models;
using Canvas.Core.Shapes;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Indicators;
using Terminal.Core.Models;
using Ib = InteractiveBrokers;
using Sc = Schwab;
using Scm = Schwab.Messages;
using Sim = Simulation;

namespace Terminal.Components
{
  public partial class OptionPageComponent
  {
    [Inject] IConfiguration Configuration { get; set; }

    public virtual PageComponent View { get; set; }
    public virtual PerformanceIndicator Performance { get; set; }
    public virtual InstrumentModel Instrument { get; set; } = new InstrumentModel
    {
      Name = "SPY",
      Type = InstrumentEnum.Shares
    };

    /// <summary>
    /// Setup views and adapters
    /// </summary>
    /// <param name="action"></param>
    /// <param name="span"></param>
    /// <returns></returns>
    public virtual async Task OnLoad(Func<PointModel, Task> action, IList<Shape> groups = null, TimeSpan? span = null)
    {
      await CreateViews(groups);

      Performance = new PerformanceIndicator { Name = nameof(Performance) };

      View.OnPreConnect = () =>
      {
        View.Adapters["Sim"] = CreateSimAccount();
        //View.Adapters["Ib"] = CreateIbAccount();
        //View.Adapters["Sc"] = CreateScAccount();
      };

      View.OnPostConnect = () =>
      {
        var account = View.Adapters["Sim"].Account;

        View.DealsView.UpdateItems(account.Deals);
        View.OrdersView.UpdateItems(account.Orders.Values);
        View.PositionsView.UpdateItems(account.Positions.Values);

        Instrument.TimeFrame = span;

        account
          .Instruments
          .Values
          .ForEach(o => o.PointGroups.CollectionChanged += (_, e) => e
            .NewItems
            .OfType<PointModel>()
            .ForEach(async o => await action(o)));
      };
    }

    /// <summary>
    /// Charts setup
    /// </summary>
    /// <returns></returns>
    public virtual async Task CreateViews(IList<Shape> groups)
    {
      var indAreas = new Shape();
      var indCharts = Enumerable.Range(0, 5).Select(o => new Shape()).ToList();

      indCharts[0].Groups["OptionDelta"] = new AreaShape { Component = new ComponentModel { Color = SKColors.DeepSkyBlue } };
      indCharts[0].Groups["BasisDelta"] = new AreaShape { Component = new ComponentModel { Color = SKColors.OrangeRed } };
      indCharts[1].Groups["Vega"] = new AreaShape { Component = new ComponentModel { Color = SKColors.LimeGreen } };
      indCharts[2].Groups["PutBids"] = new AreaShape { Component = new ComponentModel { Color = SKColors.DeepSkyBlue } };
      indCharts[2].Groups["PutAsks"] = new AreaShape { Component = new ComponentModel { Color = SKColors.OrangeRed } };
      indCharts[3].Groups["CallBids"] = new AreaShape { Component = new ComponentModel { Color = SKColors.DeepSkyBlue } };
      indCharts[3].Groups["CallAsks"] = new AreaShape { Component = new ComponentModel { Color = SKColors.OrangeRed } };
      indCharts[4].Groups["PutRatio"] = new LineShape { Component = new ComponentModel { Size = 5, Color = SKColors.OrangeRed } };
      indCharts[4].Groups["CallRatio"] = new LineShape { Component = new ComponentModel { Size = 5, Color = SKColors.DeepSkyBlue } };
      indCharts[4].Groups["PcRatio"] = new LineShape { Component = new ComponentModel { Size = 5, Color = SKColors.LimeGreen } };

      indCharts = [.. (groups ?? []), .. indCharts];

      for (var i = 0; i < indCharts.Count; i++)
      {
        indAreas.Groups[$"{i}"] = indCharts[i];
      }

      await View.ChartsView.Create(indAreas);

      var pnlAreas = new Shape();
      var pnlCharts = new Shape();

      pnlCharts.Groups["PnL"] = new LineShape { Component = new ComponentModel { Color = SKColors.OrangeRed, Size = 5 } };
      pnlCharts.Groups["Balance"] = new AreaShape { Component = new ComponentModel { Color = SKColors.Black } };
      pnlAreas.Groups["Performance"] = pnlCharts;

      await View.ReportsView.Create(pnlAreas);
    }

    /// <summary>
    /// Setup simulation account
    /// </summary>
    /// <returns></returns>
    public virtual Sim.Adapter CreateSimAccount()
    {
      var account = new Account
      {
        Balance = 25000,
        Instruments = new ConcurrentDictionary<string, InstrumentModel>
        {
          [Instrument.Name] = Instrument
        }
      };

      return new Sim.Adapter
      {
        Account = account,
        Source = Configuration["Simulation:Source"]
      };
    }

    /// <summary>
    /// Setup Schwab account
    /// </summary>
    /// <returns></returns>
    public virtual Sc.Adapter CreateScAccount()
    {
      var account = new Account
      {
        Descriptor = Configuration["Schwab:Account"],
        Instruments = new ConcurrentDictionary<string, InstrumentModel>
        {
          [Instrument.Name] = Instrument
        }
      };

      return new Sc.Adapter
      {
        Account = account,
        Scope = new Scm.ScopeMessage
        {
          AccessToken = Configuration["Schwab:AccessToken"],
          RefreshToken = Configuration["Schwab:RefreshToken"],
          ConsumerKey = Configuration["Schwab:ConsumerKey"],
          ConsumerSecret = Configuration["Schwab:ConsumerSecret"],
        }
      };
    }

    /// <summary>
    /// Setup Interactive Brokers account
    /// </summary>
    /// <returns></returns>
    public virtual Ib.Adapter CreateIbAccount()
    {
      var account = new Account
      {
        Descriptor = Configuration["InteractiveBrokers:Account"],
        Instruments = new ConcurrentDictionary<string, InstrumentModel>
        {
          [Instrument.Name] = Instrument
        }
      };

      return new Ib.Adapter
      {
        Account = account
      };
    }

    /// <summary>
    /// Get position delta
    /// </summary>
    /// <param name="o"></param>
    /// <returns></returns>
    public virtual double GetDelta(OrderModel o)
    {
      var volume = o.Transaction?.Volume;
      var leverage = o.Transaction?.Instrument?.Leverage;
      var delta = o.Transaction?.Instrument?.Derivative?.Variable?.Delta;
      var side = o.Side is OrderSideEnum.Buy ? 1.0 : -1.0;

      return ((delta ?? volume) * leverage * side) ?? 0;
    }

    /// <summary>
    /// Process tick data
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    public virtual async Task OnUpdate(PointModel point, Action<IList<InstrumentModel>> action)
    {
      var adapter = View.Adapters["Sim"];
      var account = adapter.Account;
      var options = await GetOptions(point);

      action(options);

      var chartPoints = new List<KeyValuePair<string, PointModel>>();
      var reportPoints = new List<KeyValuePair<string, PointModel>>();
      var gamma = options.Sum(o => o.Derivative.Variable.Gamma);
      var vega = options.Sum(o => o.Derivative.Variable.Vega);
      var putBids = options.Where(o => o.Derivative.Side is OptionSideEnum.Put).Sum(o => o.Point.BidSize ?? 0);
      var putAsks = options.Where(o => o.Derivative.Side is OptionSideEnum.Put).Sum(o => o.Point.AskSize ?? 0);
      var callBids = options.Where(o => o.Derivative.Side is OptionSideEnum.Call).Sum(o => o.Point.BidSize ?? 0);
      var callAsks = options.Where(o => o.Derivative.Side is OptionSideEnum.Call).Sum(o => o.Point.AskSize ?? 0);
      var performance = Performance.Calculate([account]);
      var putRatio = putBids - putAsks;
      var callRatio = callBids - callAsks;
      var basisDelta = account
        .Positions
        .Values
        .Where(o => o.Transaction.Instrument.Derivative is null)
        .Sum(GetDelta);

      var optionDelta = account
        .Positions
        .Values
        .Where(o => o.Transaction.Instrument.Derivative is not null)
        .Sum(GetDelta);

      chartPoints.Add(KeyValuePair.Create("OptionDelta", new PointModel { Time = point.Time, Last = optionDelta }));
      chartPoints.Add(KeyValuePair.Create("BasisDelta", new PointModel { Time = point.Time, Last = basisDelta }));
      chartPoints.Add(KeyValuePair.Create("Vega", new PointModel { Time = point.Time, Last = vega }));
      chartPoints.Add(KeyValuePair.Create("PutBids", new PointModel { Time = point.Time, Last = putBids }));
      chartPoints.Add(KeyValuePair.Create("PutAsks", new PointModel { Time = point.Time, Last = -putAsks }));
      chartPoints.Add(KeyValuePair.Create("PutRatio", new PointModel { Time = point.Time, Last = putRatio }));
      chartPoints.Add(KeyValuePair.Create("CallBids", new PointModel { Time = point.Time, Last = callBids }));
      chartPoints.Add(KeyValuePair.Create("CallAsks", new PointModel { Time = point.Time, Last = -callAsks }));
      chartPoints.Add(KeyValuePair.Create("CallRatio", new PointModel { Time = point.Time, Last = callRatio }));
      chartPoints.Add(KeyValuePair.Create("PcRatio", new PointModel { Time = point.Time, Last = putRatio - callRatio }));
      reportPoints.Add(KeyValuePair.Create("Balance", new PointModel { Time = point.Time, Last = account.Balance }));
      reportPoints.Add(KeyValuePair.Create("PnL", new PointModel { Time = point.Time, Last = performance.Point.Last }));

      await View.ChartsView.UpdateItems(chartPoints);
      await View.ReportsView.UpdateItems(reportPoints);
      await View.DealsView.UpdateItems(account.Deals);
      await View.OrdersView.UpdateItems(account.Orders.Values);
      await View.PositionsView.UpdateItems(account.Positions.Values);
    }

    /// <summary>
    /// Get option chain
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    public virtual async Task<IList<InstrumentModel>> GetOptions(PointModel point)
    {
      var adapter = View.Adapters["Sim"];
      var account = adapter.Account;
      var optionArgs = new OptionScreenerModel
      {
        Name = Instrument.Name,
        MinDate = point.Time,
        MaxDate = point.Time.Value.AddDays(1),
        Point = point
      };

      var options = await adapter.GetOptions(optionArgs, []);
      var nextDayOptions = options
        .Data
        .OrderBy(o => o.Derivative.Expiration)
        .ThenBy(o => o.Derivative.Strike)
        .ThenBy(o => o.Derivative.Side)
        .GroupBy(o => o.Derivative.Expiration)
        .ToDictionary(o => o.Key, o => o.ToList())
        .FirstOrDefault()
        .Value;

      return nextDayOptions;
    }

    /// <summary>
    /// Close positions
    /// </summary>
    /// <param name="instrumentType"></param>
    /// <returns></returns>
    public async Task ClosePositions(InstrumentEnum? instrumentType = null)
    {
      var adapter = View.Adapters["Sim"];

      foreach (var position in adapter.Account.Positions.Values.ToList())
      {
        var order = new OrderModel
        {
          Side = position.Side is OrderSideEnum.Buy ? OrderSideEnum.Sell : OrderSideEnum.Buy,
          Type = OrderTypeEnum.Market,
          Transaction = new()
          {
            Volume = position.Transaction.Volume,
            Instrument = position.Transaction.Instrument
          }
        };

        var insEmpty = instrumentType is null;
        var insShares = instrumentType is InstrumentEnum.Shares && position.Transaction.Instrument.Derivative is null;
        var insOptions = instrumentType is InstrumentEnum.Options && position.Transaction.Instrument.Derivative is not null;

        if (insEmpty || insShares || insOptions)
        {
          await adapter.CreateOrders(order);
        }
      }
    }

    /// <summary>
    /// Create short straddle strategy
    /// </summary>
    /// <param name="point"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public virtual IList<OrderModel> GetCondor(PointModel point, IList<InstrumentModel> options)
    {
      var shortPut = options
        .Where(o => o.Derivative.Side is OptionSideEnum.Put)
        .Where(o => o.Derivative.Strike >= point.Last)
        .FirstOrDefault();

      var longPut = options
        .Where(o => o.Derivative.Side is OptionSideEnum.Put)
        .Where(o => o.Derivative.Strike < shortPut.Derivative.Strike - 3)
        .LastOrDefault();

      var shortCall = options
        .Where(o => o.Derivative.Side is OptionSideEnum.Call)
        .Where(o => o.Derivative.Strike >= point.Last)
        .FirstOrDefault();

      var longCall = options
        .Where(o => o.Derivative.Side is OptionSideEnum.Call)
        .Where(o => o.Derivative.Strike > shortCall.Derivative.Strike + 3)
        .FirstOrDefault();

      var order = new OrderModel
      {
        Type = OrderTypeEnum.Market,
        Instruction = InstructionEnum.Group,
        Orders =
        [
          new OrderModel
          {
            Side = OrderSideEnum.Buy,
            Instruction = InstructionEnum.Side,
            Transaction = new() { Volume = 1, Instrument = longPut }
          },
          new OrderModel
          {
            Side = OrderSideEnum.Buy,
            Instruction = InstructionEnum.Side,
            Transaction = new() { Volume = 1, Instrument = longCall }
          },
          new OrderModel
          {
            Side = OrderSideEnum.Sell,
            Instruction = InstructionEnum.Side,
            Transaction = new() { Volume = 1, Instrument = shortPut }
          },
          new OrderModel
          {
            Side = OrderSideEnum.Sell,
            Instruction = InstructionEnum.Side,
            Transaction = new() { Volume = 1, Instrument = shortCall }
          }
        ]
      };

      return [order];
    }

    /// <summary>
    /// Create short straddle strategy
    /// </summary>
    /// <param name="point"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public virtual IList<OrderModel> GetShortStraddle(PointModel point, IList<InstrumentModel> options)
    {
      var shortPut = options
        .Where(o => o.Derivative.Side is OptionSideEnum.Put)
        .Where(o => o.Derivative.Strike >= point.Last)
        .FirstOrDefault();

      var shortCall = options
        .Where(o => o.Derivative.Side is OptionSideEnum.Call)
        .Where(o => o.Derivative.Strike >= point.Last)
        .FirstOrDefault();

      var order = new OrderModel
      {
        Type = OrderTypeEnum.Market,
        Instruction = InstructionEnum.Group,
        Orders =
        [
          new OrderModel
          {
            Side = OrderSideEnum.Sell,
            Instruction = InstructionEnum.Side,
            Price = shortPut.Point.Bid,
            Transaction = new() { Volume = 1, Instrument = shortPut }
          },
          new OrderModel
          {
            Side = OrderSideEnum.Sell,
            Instruction = InstructionEnum.Side,
            Price = shortCall.Point.Bid,
            Transaction = new() { Volume = 1, Instrument = shortCall }
          }
        ]
      };

      return [order];
    }

    /// <summary>
    /// Hedge each delta change with shares
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    public virtual IList<OrderModel> GetShareHedge(PointModel point)
    {
      var account = View.Adapters["Sim"].Account;
      var basisDelta = Math.Round(account
        .Positions
        .Values
        .Where(o => o.Transaction.Instrument.Derivative is null)
        .Sum(GetDelta), MidpointRounding.ToZero);

      var optionDelta = Math.Round(account
        .Positions
        .Values
        .Where(o => o.Transaction.Instrument.Derivative is not null)
        .Sum(GetDelta), MidpointRounding.ToZero);

      var delta = Math.Abs(optionDelta) - Math.Abs(basisDelta);

      if (Math.Abs(delta) > 0)
      {
        var order = new OrderModel
        {
          Type = OrderTypeEnum.Market,
          Side = delta < 0 ? OrderSideEnum.Buy : OrderSideEnum.Sell,
          Transaction = new() { Volume = Math.Abs(delta), Instrument = point.Instrument }
        };

        return [order];
      }

      return [];
    }

    /// <summary>
    /// Inverse share position to match option delta
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    public virtual IList<OrderModel> GetShareInverse(PointModel point)
    {
      var account = View.Adapters["Sim"].Account;
      var basisDelta = account
        .Positions
        .Values
        .Where(o => o.Transaction.Instrument.Derivative is null)
        .Sum(GetDelta);

      var optionDelta = account
        .Positions
        .Values
        .Where(o => o.Transaction.Instrument.Derivative is not null)
        .Sum(GetDelta);

      var isOversold = basisDelta < 0 && optionDelta > 0;
      var isOverbought = basisDelta > 0 && optionDelta < 0;

      if (basisDelta is 0 || isOversold || isOverbought)
      {
        var order = new OrderModel
        {
          Type = OrderTypeEnum.Market,
          Side = optionDelta > 0 ? OrderSideEnum.Buy : OrderSideEnum.Sell,
          Transaction = new() { Volume = 100, Instrument = point.Instrument }
        };

        return [order];
      }

      return [];
    }

    /// <summary>
    /// Create short straddle strategy
    /// </summary>
    /// <param name="side"></param>
    /// <param name="point"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public virtual IList<OrderModel> GetCreditSpread(OptionSideEnum side, PointModel point, IList<InstrumentModel> options)
    {
      var adapter = View.Adapters["Sim"];
      var account = adapter.Account;
      var position = account.Positions.FirstOrDefault().Value;
      var sideOptions = options.Where(o => Equals(o.Derivative.Side, side));
      var order = new OrderModel
      {
        Type = OrderTypeEnum.Market,
        Orders =
        [
          new OrderModel
          {
            Side = OrderSideEnum.Sell,
            Instruction = InstructionEnum.Side,
            Transaction = new TransactionModel { Volume = 1 }
          },
          new OrderModel
          {
            Side = OrderSideEnum.Buy,
            Instruction = InstructionEnum.Side,
            Transaction = new TransactionModel { Volume = 1 }
          }
        ]
      };

      switch (side)
      {
        case OptionSideEnum.Put:

          var creditPut = order.Orders[0].Transaction.Instrument = sideOptions
            .Where(o => Math.Abs(o.Derivative.Variable.Delta ?? 0) >= 0.3)
            .FirstOrDefault();

          order.Orders[1].Transaction.Instrument = sideOptions
            .Where(o => o.Derivative.Strike < creditPut.Derivative.Strike)
            .LastOrDefault();

          break;

        case OptionSideEnum.Call:

          var creditCall = order.Orders[0].Transaction.Instrument = sideOptions
            .Where(o => Math.Abs(o.Derivative.Variable.Delta ?? 0) >= 0.3)
            .LastOrDefault();

          order.Orders[1].Transaction.Instrument = sideOptions
            .Where(o => o.Derivative.Strike > creditCall.Derivative.Strike)
            .FirstOrDefault();

          break;
      }

      return [order];
    }
  }
}
