using Canvas.Core.Models;
using Canvas.Core.Shapes;
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
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Indicators;
using Terminal.Core.Models;

namespace Terminal.Pages.Options
{
  public partial class DebitSpreadHedge
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
    /// Setup simulation account
    /// </summary>
    /// <returns></returns>
    protected virtual Adapter CreateSimAccount()
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

      return new Adapter
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
    protected async Task OnData(PointModel point)
    {
      var adapter = View.Adapters["Sim"];
      var account = adapter.Account;
      var options = await GetOptions(point);
      var chartPoints = new List<KeyValuePair<string, PointModel>>();
      var reportPoints = new List<KeyValuePair<string, PointModel>>();
      var performance = Performance.Calculate([account]);
      var strike = GetStrike(point, options);

      if (account.Orders.Count is 0 && account.Positions.Count is 0)
      {
        var order = GetNextOrder(strike.Key.Value, point, options);
        var orderResponse = await adapter.CreateOrders(order);
      }

      if (account.Positions.Count > 0 && Equals(Math.Round(point.Last.Value), Math.Round(strike.Key.Value)) is false)
      {
        var nextOrder = GetNextOrder(strike.Key.Value, point, options);
        var curOrder = account.Positions.Values.First();
        var nextSide = nextOrder.Transaction.Instrument.Derivative.Side;
        var curSide = curOrder.Transaction.Instrument.Derivative.Side;

        if (!Equals(nextSide, curSide))
        {
          await ClosePositions();
          var orderResponse = await adapter.CreateOrders(nextOrder);
        }
      }

      chartPoints.Add(KeyValuePair.Create("Range", new PointModel { Time = point.Time, Last = strike.Key }));
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
    /// Get strike with max gamma
    /// </summary>
    /// <param name="point"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    protected virtual KeyValuePair<double?, double> GetStrike(PointModel point, IList<InstrumentModel> options)
    {
      var res = options
        .GroupBy(o => o.Derivative.Strike)
        .ToDictionary(
          option => option.Key,
          option =>
            option.Where(o => o.Derivative.Side is OptionSideEnum.Call).Sum(v => v.Derivative.Variable.Gamma ?? 0) +
            option.Where(o => o.Derivative.Side is OptionSideEnum.Put).Sum(v => v.Derivative.Variable.Gamma ?? 0))
        .OrderByDescending(o => o.Value)
        .FirstOrDefault();

      return res;
    }

    /// <summary>
    /// Create short straddle strategy
    /// </summary>
    /// <param name="strike"></param>
    /// <param name="point"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    protected virtual OrderModel GetNextOrder(double strike, PointModel point, IList<InstrumentModel> options)
    {
      var side = point.Last < strike ? OptionSideEnum.Call : OptionSideEnum.Put;
      var nextOption = options
        .Where(o => Equals(o.Derivative.Side, side))
        .Where(o => o.Derivative.Strike >= point.Last)
        .FirstOrDefault();

      var order = new OrderModel
      {
        Side = OrderSideEnum.Buy,
        Type = OrderTypeEnum.Market,
        Transaction = new() { Volume = 1, Instrument = nextOption }
      };

      return order;
    }

    /// <summary>
    /// Close all positions
    /// </summary>
    protected async Task ClosePositions()
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

        await adapter.CreateOrders(order);
      }
    }
  }
}
