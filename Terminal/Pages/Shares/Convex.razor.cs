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
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Indicators;
using Terminal.Core.Models;
using Terminal.Services;

namespace Terminal.Pages.Shares
{
  public partial class Convex
  {
    [Inject] IConfiguration Configuration { get; set; }

    double step = 5;
    double stepBase = 5;
    double stepSide = -1;
    string asset = "GOOG";

    protected virtual double? Price { get; set; }
    protected virtual ControlsComponent View { get; set; }
    protected virtual ChartsComponent ChartsView { get; set; }
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
        await ChartsView.Create("Prices");
        await PerformanceView.Create("Performance");

        InstanceService<SubscriptionService>.Instance.OnUpdate += state =>
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
        State = new ConcurrentDictionary<string, StateModel>
        {
          [asset] = new StateModel { Instrument = new InstrumentModel { Name = asset, TimeFrame = TimeSpan.FromMinutes(1) } },
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
        .ForEach(adapter => adapter.DataStream += message => OnData(message.Next));
    }

    protected async void OnData(PointModel point)
    {
      Price ??= point.Last;

      var adapter = View.Adapters["Prime"];
      var account = adapter.Account;
      var summary = account.State[asset];
      var instrument = summary.Instrument;
      var series = summary.Points;
      var performance = Performance.Update([account]);

      if (account.Positions.Count > 0)
      {
        var closures = new List<OrderModel>();
        var pos = account.Positions.Values.First();
        var isIncrease = point.Last - Price > step;
        var isDecrease = Price - point.Last > step;
        var canIncrease = step > 0 && pos.Side is not OrderSideEnum.Long;
        var canDecrease = step > 0 && pos.Side is not OrderSideEnum.Short;

        if (isIncrease && canIncrease)
        {
          closures = await ClosePositions(o => o.Side is OrderSideEnum.Short);
          await OpenPositions(point, 1, OrderSideEnum.Long);
        }

        if (isDecrease && canDecrease)
        {
          closures = await ClosePositions(o => o.Side is OrderSideEnum.Long);
          await OpenPositions(point, 1, OrderSideEnum.Short);
        }

        // Progressive increase

        if (isIncrease || isDecrease)
        {
          step = closures.Count is not 0 ? stepBase : Math.Max(Math.Abs(stepSide), step + stepSide);
          Price = point.Last;
        }
      }

      if (account.Positions.Count is 0)
      {
        await OpenPositions(point, 1, OrderSideEnum.Long);
      }

      DealsView.UpdateItems([.. View.Adapters.Values]);
      OrdersView.UpdateItems([.. View.Adapters.Values]);
      PositionsView.UpdateItems([.. View.Adapters.Values]);
      ChartsView.UpdateItems(point.Time.Value.Ticks, "Prices", "Bars", ChartsView.GetShape<CandleShape>(point));
      PerformanceView.UpdateItems(point.Time.Value.Ticks, "Performance", "Balance", new AreaShape { Y = account.Balance });
      PerformanceView.UpdateItems(point.Time.Value.Ticks, "Performance", "PnL", PerformanceView.GetShape<LineShape>(performance.Point, SKColors.OrangeRed));
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
        Transaction = new() { Instrument = instrument }
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
