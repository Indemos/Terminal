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
using Terminal.Core.Extensions;
using Terminal.Core.Indicators;
using Terminal.Core.Models;

namespace Terminal.Pages.Shares
{
  public partial class RangeBar
  {
    [Inject] IConfiguration Configuration { get; set; }

    protected virtual double Price { get; set; }
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

        Performance = new PerformanceIndicator { Name = nameof(Performance) };

        View.OnPreConnect = CreateAccounts;
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
      var indAreas = new Shape();
      var indCharts = Enumerable.Range(0, 1).Select(o => new Shape()).ToList();

      indCharts[0].Groups["Price"] = new LineShape();

      for (var i = 0; i < indCharts.Count; i++)
      {
        indAreas.Groups[$"{i}"] = indCharts[i];
      }

      await View.ChartsView.Create(indAreas);

      var pnlAreas = new Shape();
      var pnlCharts = new Shape();

      pnlCharts.Groups["Balance"] = new AreaShape { Component = new ComponentModel { Color = SKColors.Black } };
      pnlCharts.Groups["PnL"] = new LineShape { Component = new ComponentModel { Color = SKColors.OrangeRed, Size = 2 } };
      pnlAreas.Groups["Performance"] = pnlCharts;

      await View.ReportsView.Create(pnlAreas);
    }

    /// <summary>
    /// Setup simulation account
    /// </summary>
    /// <returns></returns>
    protected virtual void CreateAccounts()
    {
      var account = new Account
      {
        Balance = 25000,
        Instruments = new ConcurrentDictionary<string, InstrumentModel>
        {
          [Instrument.Name] = Instrument
        }
      };

      View.Adapters["Sim"] = new Adapter
      {
        Speed = 1,
        Account = account,
        Source = Configuration["Simulation:Source"]
      };

      View
        .Adapters
        .Values
        .ForEach(adapter => adapter.PointStream += async message => await OnData(message.Next));
    }

    /// <summary>
    /// Process tick data
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    protected async Task OnData(PointModel point)
    {
      var step = 0.50;
      var adapter = View.Adapters["Sim"];
      var account = adapter.Account;
      var chartPoints = new List<KeyValuePair<string, PointModel>>();
      var reportPoints = new List<KeyValuePair<string, PointModel>>();
      var performance = Performance.Calculate([account]);

      Price = Price.Is(0) ? point.Last.Value : Price;

      if (Math.Abs((point.Last - Price).Value) > step)
      {
        var side = OrderSideEnum.Buy;
        var position = account.Positions.FirstOrDefault().Value;

        if (point.Last < Price)
        {
          side = OrderSideEnum.Sell;
        }

        if (position is not null && Equals(side, position.Side) is false)
        {
          await ClosePositions();
        }

        var order = new OrderModel
        {
          Side = side,
          Type = OrderTypeEnum.Market,
          Transaction = new() { Volume = 100, Instrument = Instrument }
        };

        Price = point.Last.Value;
        chartPoints.Add(KeyValuePair.Create("Price", new PointModel { Time = point.Time, Last = Price }));

        await adapter.CreateOrders(order);
        View.ChartsView.UpdateItems(chartPoints);
      }

      reportPoints.Add(KeyValuePair.Create("Balance", new PointModel { Time = point.Time, Last = account.Balance }));
      reportPoints.Add(KeyValuePair.Create("PnL", new PointModel { Time = point.Time, Last = performance.Point.Last }));

      View.ReportsView.UpdateItems(reportPoints);
      View.DealsView.UpdateItems(account.Deals);
      View.OrdersView.UpdateItems(account.Orders.Values);
      View.PositionsView.UpdateItems(account.Positions.Values);
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
