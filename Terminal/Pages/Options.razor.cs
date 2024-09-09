using Canvas.Core.Models;
using Canvas.Core.Shapes;
using IBApi;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Components;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Indicators;
using Terminal.Core.Models;
using Ib = InteractiveBrokers;
using Sc = Schwab;
using Scm = Schwab.Messages;
using Sim = Simulation;

namespace Terminal.Pages
{
  public partial class Options
  {
    [Inject] IConfiguration Configuration { get; set; }

    protected virtual PageComponent View { get; set; }
    protected virtual PerformanceIndicator Performance { get; set; }
    protected virtual InstrumentModel Instrument { get; set; } = new InstrumentModel
    {
      Name = "SPY",
      Type = InstrumentEnum.Shares
    };

    /// <summary>
    /// Setup views and adapters
    /// </summary>
    /// <param name="setup"></param>
    /// <returns></returns>
    protected override async Task OnAfterRenderAsync(bool setup)
    {
      if (setup)
      {
        await CreateViews();

        Performance = new PerformanceIndicator { Name = "Balance" };

        View.OnPreConnect = () =>
        {
          View.Adapters["Sim"] = CreateSimAccount();
          //View.Adapters["Ib"] = CreateIbAccount();
          //View.Adapters["Sc"] = CreateScAccount();
        };

        View.OnPostConnect = () =>
        {
          var order = new OrderModel
          {
            Transaction = new TransactionModel
            {
              Instrument = Instrument,
            }
          };

          var account = View.Adapters["Sim"].Account;

          View.DealsView.UpdateItems(account.Deals);
          View.OrdersView.UpdateItems(account.Orders.Values);
          View.PositionsView.UpdateItems(account.Positions.Values);
        };
      }

      await base.OnAfterRenderAsync(setup);
    }

    /// <summary>
    /// Charts setup
    /// </summary>
    /// <returns></returns>
    protected virtual async Task CreateViews()
    {
      var indUp = new ComponentModel { Color = SKColors.DeepSkyBlue };
      var indDown = new ComponentModel { Color = SKColors.OrangeRed };
      var indAreas = new Shape();
      var indCharts = new Shape();

      indCharts.Groups["Ups"] = new AreaShape { Component = indUp };
      indCharts.Groups["Downs"] = new AreaShape { Component = indDown };
      indCharts.Groups["Range"] = new AreaShape { Component = indUp };
      indAreas.Groups["Prices"] = indCharts;

      await View.ChartsView.Create(indAreas);

      var pnlGain = new ComponentModel { Color = SKColors.OrangeRed, Size = 5 };
      var pnlBalance = new ComponentModel { Color = SKColors.Black };
      var pnlAreas = new Shape();
      var pnlCharts = new Shape();

      pnlCharts.Groups["PnL"] = new LineShape { Component = pnlGain };
      pnlCharts.Groups["Balance"] = new AreaShape { Component = pnlBalance };
      pnlAreas.Groups["Performance"] = pnlCharts;

      await View.ReportsView.Create(pnlAreas);
    }

    /// <summary>
    /// Setup Schwab account
    /// </summary>
    /// <returns></returns>
    protected virtual Sc.Adapter CreateScAccount()
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
    protected virtual Ib.Adapter CreateIbAccount()
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
    /// Setup simulation account
    /// </summary>
    /// <returns></returns>
    protected virtual Sim.Adapter CreateSimAccount()
    {
      var account = new Account
      {
        Balance = 25000,
        Instruments = new ConcurrentDictionary<string, InstrumentModel>
        {
          [Instrument.Name] = Instrument
        }
      };

      account
        .Instruments
        .Values
        .ForEach(o => o.Points.CollectionChanged += (_, e) => e
          .NewItems
          .OfType<PointModel>()
          .ForEach(async o => await OnData(o)));

      return new Sim.Adapter
      {
        Account = account,
        Source = Configuration["Simulation:Source"]
      };
    }

    /// <summary>
    /// Process tick data
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    private async Task OnData(PointModel point)
    {
      var simAdapter = View.Adapters["Sim"];
      var account = simAdapter.Account;
      var options = await GetOptions(point);
      var chartPoints = new List<KeyValuePair<string, PointModel>>();
      var reportPoints = new List<KeyValuePair<string, PointModel>>();
      var performance = Performance.Calculate([account]);

      if (account.Orders.Count is 0 && account.Positions.Count is 0)
      {
        var orders = GetShortStraddle(point, options);
        var orderResponse = await simAdapter.CreateOrders([.. orders]);
      }

      var basisDelta = account
        .Positions
        .Values
        .Where(o => o.Transaction.Instrument.Derivative is null)
        .Sum(getDelta);

      var optionDelta = account
        .Positions
        .Values
        .Where(o => o.Transaction.Instrument.Derivative is not null)
        .Sum(getDelta);

      if (account.Positions.Count > 0)
      {
        var orders = GetShortHedge(point, basisDelta, optionDelta);

        if (orders.Count > 0)
        {
          var orderResponse = await simAdapter.CreateOrders([.. orders]);
        }
      }

      chartPoints.Add(KeyValuePair.Create("Ups", new PointModel { Time = point.Time, Last = basisDelta }));
      chartPoints.Add(KeyValuePair.Create("Downs", new PointModel { Time = point.Time, Last = optionDelta }));
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
    protected virtual async Task<IList<InstrumentModel>> GetOptions(PointModel point)
    {
      var simAdapter = View.Adapters["Sim"];
      var account = simAdapter.Account;
      var optionArgs = new OptionScreenerModel
      {
        Name = Instrument.Name,
        MinDate = point.Time,
        MaxDate = point.Time.Value.AddDays(1),
        Point = point
      };

      var options = await simAdapter.GetOptions(optionArgs, []);
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
    /// Create short straddle strategy
    /// </summary>
    /// <param name="point"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    protected virtual IList<OrderModel> GetShortStraddle(PointModel point, IList<InstrumentModel> options)
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
    /// Create short straddle strategy
    /// </summary>
    /// <param name="point"></param>
    /// <param name="basisDelta"></param>
    /// <param name="optionDelta"></param>
    /// <returns></returns>
    protected virtual IList<OrderModel> GetShortHedge(PointModel point, double basisDelta, double optionDelta)
    {
      var account = View.Adapters["Sim"].Account;
      var gain = account.Positions.Sum(o => o.Value.GetGainEstimate());
      var delta = Math.Round(basisDelta + optionDelta);

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

    protected virtual double getDelta(OrderModel o)
    {
      var volume = o.Transaction?.Volume;
      var leverage = o.Transaction?.Instrument?.Leverage;
      var delta = o.Transaction?.Instrument?.Derivative?.Variable?.Delta;
      var side = o.Side is OrderSideEnum.Buy ? 1.0 : -1.0;

      return ((delta ?? volume) * leverage * side) ?? 0;
    }
  }
}
