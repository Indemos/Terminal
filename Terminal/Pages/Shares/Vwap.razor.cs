using Canvas.Core.Models;
using Canvas.Core.Shapes;
using Distribution.Services;
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
using Terminal.Core.Collections;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Indicators;
using Terminal.Core.Models;
using Terminal.Services;

namespace Terminal.Pages.Shares
{
  public partial class Vwap
  {
    [Inject] IConfiguration Configuration { get; set; }

    string asset = "SPY";

    protected virtual ControlsComponent View { get; set; }
    protected virtual ChartsComponent ChartsView { get; set; }
    protected virtual ChartsComponent PerformanceView { get; set; }
    protected virtual DealsComponent DealsView { get; set; }
    protected virtual OrdersComponent OrdersView { get; set; }
    protected virtual PositionsComponent PositionsView { get; set; }
    protected virtual StatementsComponent StatementsView { get; set; }
    protected virtual PerformanceIndicator Performance { get; set; }
    protected virtual VwapIndicator Range { get; set; }

    protected override async Task OnAfterRenderAsync(bool setup)
    {
      if (setup)
      {
        await ChartsView.Create("Prices");
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
        State = new Map<string, StateModel>
        {
          [asset] = new StateModel { Instrument = new InstrumentModel { Name = asset } },
        },
      };

      View.Adapters["Prime"] = new Adapter
      {
        Speed = 1,
        Account = account,
        Source = Configuration["Simulation:Source"]
      };

      Range = new VwapIndicator { Name = "Vwap" };
      Performance = new PerformanceIndicator { Name = "Balance" };

      View
        .Adapters
        .Values
        .ForEach(adapter => adapter.DataStream += message => OnData(message.Next));
    }

    protected async void OnData(PointModel point)
    {
      var adapter = View.Adapters["Prime"];
      var account = adapter.Account;
      var summary = account.State[asset];
      var instrument = summary.Instrument;
      var series = summary.Points;
      var performance = Performance.Update([account]);
      var vwap = Range.Update(summary.PointGroups);
      var comPrice = new ComponentModel { Size = 3, Color = SKColors.OrangeRed };
      var comRange = new ComponentModel { Size = 1, Color = SKColors.DimGray };

      DealsView.UpdateItems([.. View.Adapters.Values]);
      OrdersView.UpdateItems([.. View.Adapters.Values]);
      PositionsView.UpdateItems([.. View.Adapters.Values]);
      ChartsView.UpdateItems(point.Time.Value.Ticks, "Prices", "Price", new LineShape { Y = point.Last, Component = comPrice });
      ChartsView.UpdateItems(point.Time.Value.Ticks, "Prices", "Vwap", new LineShape { Y = vwap.Point.Last, Component = comRange });
      ChartsView.UpdateItems(point.Time.Value.Ticks, "Prices", "Vwap Low", new LineShape { Y = vwap.Point.Bar.Low, Component = comRange });
      ChartsView.UpdateItems(point.Time.Value.Ticks, "Prices", "Vwap High", new LineShape { Y = vwap.Point.Bar.High, Component = comRange });
      PerformanceView.UpdateItems(point.Time.Value.Ticks, "Performance", "Balance", new AreaShape { Y = account.Balance });
      PerformanceView.UpdateItems(point.Time.Value.Ticks, "Performance", "PnL", PerformanceView.GetShape<LineShape>(performance.Point, SKColors.OrangeRed));

      var crossTopDown = point.Last < vwap.Point.Bar.High && summary.PointGroups.TakeLast(5).Any(o => o.Last > o.Series["Vwap"].Bar.High);
      var crossBottomUp = point.Last > vwap.Point.Bar.Low && summary.PointGroups.TakeLast(5).Any(o => o.Last < o.Series["Vwap"].Bar.Low);
      var pos = account.Positions.Values.FirstOrDefault();

      if (crossTopDown && pos?.Side is not OrderSideEnum.Short)
      {
        await adapter.ClearOrders([.. account.Orders.Values]);
        await ClosePositions(o => o.Side is OrderSideEnum.Long);
        await OpenPositions(point, 1, OrderSideEnum.Short);
        return;
      }

      if (crossBottomUp && pos?.Side is not OrderSideEnum.Long)
      {
        await adapter.ClearOrders([.. account.Orders.Values]);
        await ClosePositions(o => o.Side is OrderSideEnum.Short);
        await OpenPositions(point, 1, OrderSideEnum.Long);
        return;
      }
    }

    /// <summary>
    /// Open positions
    /// </summary>
    /// <param name="point"></param>
    /// <param name="volume"></param>
    /// <param name="side"></param>
    /// <returns></returns>
    protected async Task OpenPositions(PointModel point, double volume, OrderSideEnum side)
    {
      var adapter = View.Adapters["Prime"];
      var account = adapter.Account;
      var summary = account.State[asset];
      var instrument = summary.Instrument;

      var order = new OrderModel
      {
        Side = side,
        Volume = volume,
        Type = OrderTypeEnum.Market,
        Transaction = new() { Instrument = instrument },
        Orders = [
          new OrderModel
          {
            Side = side is OrderSideEnum.Long ? OrderSideEnum.Short : OrderSideEnum.Long,
            Price = side is OrderSideEnum.Long ? point.Last - 0.5 : point.Last + 0.5,
            Volume = volume,
            Type = OrderTypeEnum.Stop,
            Transaction = new() { Instrument = instrument }
          }
        ]
      };

      await adapter.SendOrders(order);
    }

    /// <summary>
    /// Close positions
    /// </summary>
    /// <param name="condition"></param>
    /// <returns></returns>
    protected async Task<List<OrderModel>> ClosePositions(Func<OrderModel, bool> condition = null)
    {
      var response = new List<OrderModel>();
      var adapter = View.Adapters["Prime"];
      var account = adapter.Account;

      foreach (var position in adapter.Account.Positions.Values.ToList())
      {
        if (condition is null || condition(position))
        {
          var order = new OrderModel
          {
            Volume = position.Volume,
            Side = position.Side is OrderSideEnum.Long ? OrderSideEnum.Short : OrderSideEnum.Long,
            Type = OrderTypeEnum.Market,
            Transaction = new()
            {
              Instrument = position.Transaction.Instrument
            }
          };

          await adapter.SendOrders(order);

          response.Add(order);
        }
      }

      return response;
    }
  }
}
