using Canvas.Core.Models;
using Canvas.Core.Shapes;
using Distribution.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using MudBlazor;
using Simulation;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Components;
using Terminal.Core.Collections;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Indicators;
using Terminal.Core.Models;
using Terminal.Services;

namespace Terminal.Pages.Shares
{
  public partial class Balance
  {
    [Inject] IConfiguration Configuration { get; set; }

    double step = 5;
    string assetX = "GOOG";
    string assetY = "GOOGL";

    protected virtual double? PriceX { get; set; }
    protected virtual double? PriceY { get; set; }
    protected virtual ControlsComponent View { get; set; }
    protected virtual ChartsComponent PointsView { get; set; }
    protected virtual ChartsComponent DollarsView { get; set; }
    protected virtual ChartsComponent PerformanceView { get; set; }
    protected virtual DealsComponent DealsView { get; set; }
    protected virtual OrdersComponent OrdersView { get; set; }
    protected virtual PositionsComponent PositionsView { get; set; }
    protected virtual StatementsComponent StatementsView { get; set; }
    protected virtual PerformanceIndicator Performance { get; set; }

    protected override async Task OnAfterRenderAsync(bool setup)
    {
      if (setup)
      {
        await PointsView.Create("Points");
        await DollarsView.Create("Dollars");
        await PerformanceView.Create("Performance");

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

    protected virtual void CreateAccounts()
    {
      var account = new Account
      {
        Balance = 25000,
        States = new Map<string, SummaryModel>
        {
          [assetX] = new SummaryModel { Instrument = new InstrumentModel { Name = assetX } },
          [assetY] = new SummaryModel { Instrument = new InstrumentModel { Name = assetY } }
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
        .ForEach(adapter => adapter.Stream += message => OnData(message.Next));
    }

    protected async void OnData(PointModel point)
    {
      var adapter = View.Adapters["Prime"];
      var account = adapter.Account;
      var summaryX = account.States[assetX];
      var summaryY = account.States[assetY];
      var instrumentX = summaryX.Instrument;
      var instrumentY = summaryY.Instrument;
      var seriesX = summaryX.Points;
      var seriesY = summaryY.Points;

      if (seriesX.Count is 0 || seriesY.Count is 0)
      {
        return;
      }

      var performance = Performance.Update([account]);
      var xPoint = seriesX.Last();
      var yPoint = seriesY.Last();

      if (account.Positions.Count is not 0)
      {
        var closures = new List<OrderModel>();
        var isX = point.Last - PriceX > step;
        var isY = PriceY - point.Last > step;

        if (isX)
        {
          await ClosePositions(o => o.Side is OrderSideEnum.Short);
          await OpenPositions(instrumentX, 1, OrderSideEnum.Long);
          PriceX = instrumentX.Point.Last;
        }

        if (isY)
        {
          await ClosePositions(o => o.Side is OrderSideEnum.Long);
          await OpenPositions(instrumentY, 1, OrderSideEnum.Short);
          PriceY = instrumentY.Point.Last;
        }
      }

      var ups = account.Positions.Values.FirstOrDefault(o => o.Side is OrderSideEnum.Long);
      var downs = account.Positions.Values.FirstOrDefault(o => o.Side is OrderSideEnum.Short);

      if (ups is null)
      {
        await OpenPositions(instrumentX, 1, OrderSideEnum.Long);
        PriceX = instrumentX.Point.Last;
      }

      if (downs is null)
      {
        await OpenPositions(instrumentY, 1, OrderSideEnum.Short);
        PriceY = instrumentY.Point.Last;
      }

      var upCom = new ComponentModel { Color = SKColors.DeepSkyBlue };
      var downCom = new ComponentModel { Color = SKColors.OrangeRed };

      DealsView.UpdateItems([.. View.Adapters.Values]);
      OrdersView.UpdateItems([.. View.Adapters.Values]);
      PositionsView.UpdateItems([.. View.Adapters.Values]);
      PointsView.UpdateItems(point.Time.Value.Ticks, "Points", "Longs", new AreaShape { Y = ups?.GetEstimate() ?? 0, Component = upCom });
      PointsView.UpdateItems(point.Time.Value.Ticks, "Points", "Shorts", new AreaShape { Y = downs?.GetEstimate() ?? 0, Component = downCom });
      DollarsView.UpdateItems(point.Time.Value.Ticks, "Dollars", "Longs", new AreaShape { Y = ups?.GetValueEstimate() ?? 0, Component = upCom });
      DollarsView.UpdateItems(point.Time.Value.Ticks, "Dollars", "Shorts", new AreaShape { Y = downs?.GetValueEstimate() ?? 0, Component = downCom });
      PerformanceView.UpdateItems(point.Time.Value.Ticks, "Performance", "Balance", new AreaShape { Y = account.Balance });
      PerformanceView.UpdateItems(point.Time.Value.Ticks, "Performance", "PnL", PerformanceView.GetShape<LineShape>(performance.Point, SKColors.OrangeRed));
    }

    /// <summary>
    /// Open positions
    /// </summary>
    protected async Task OpenPositions(InstrumentModel instrument, double volume, OrderSideEnum side)
    {
      var adapter = View.Adapters["Prime"];
      var account = adapter.Account;
      var order = new OrderModel
      {
        Side = side,
        Amount = volume,
        Type = OrderTypeEnum.Market,
        Instrument = instrument
      };

      await adapter.SendOrder(order);
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
            Amount = position.Amount,
            Type = OrderTypeEnum.Market,
            Instrument = position.Instrument,
            Side = position.Side is OrderSideEnum.Long ? OrderSideEnum.Short : OrderSideEnum.Long
          };

          await adapter.SendOrder(order);
        }
      }
    }
  }
}
