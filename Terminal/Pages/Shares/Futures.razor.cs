using Canvas.Core.Models;
using Canvas.Core.Shapes;
using Distribution.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Simulation;
using SkiaSharp;
using System;
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
  public partial class Futures
  {
    [Inject] IConfiguration Configuration { get; set; }

    /// <summary>
    /// Strategy
    /// </summary>
    protected virtual ControlsComponent View { get; set; }
    protected virtual ChartsComponent LeaderView { get; set; }
    protected virtual ChartsComponent FollowerView { get; set; }
    protected virtual ChartsComponent IndicatorsView { get; set; }
    protected virtual ChartsComponent PerformanceView { get; set; }
    protected virtual DealsComponent DealsView { get; set; }
    protected virtual OrdersComponent OrdersView { get; set; }
    protected virtual PositionsComponent PositionsView { get; set; }
    protected virtual StatementsComponent StatementsView { get; set; }
    protected virtual PerformanceIndicator Performance { get; set; }
    protected virtual IDictionary<string, ScaleIndicator> Scales { get; set; }
    protected virtual PointModel PreviousLeader { get; set; }
    protected virtual PointModel PreviousFollower { get; set; }

    protected override async Task OnAfterRenderAsync(bool setup)
    {
      if (setup)
      {
        await LeaderView.Create("Prices");
        await FollowerView.Create("Prices");
        await IndicatorsView.Create("Indicators");
        await PerformanceView.Create("Performance");

        LeaderView.Composers.ForEach(o => o.ShowIndex = i => GetDateByIndex(o.Items, (int)i));
        FollowerView.Composers.ForEach(o => o.ShowIndex = i => GetDateByIndex(o.Items, (int)i));
        IndicatorsView.Composers.ForEach(o => o.ShowIndex = i => GetDateByIndex(o.Items, (int)i));
        PerformanceView.Composers.ForEach(o => o.ShowIndex = i => GetDateByIndex(o.Items, (int)i));

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

    /// <summary>
    /// Time axis renderer
    /// </summary>
    /// <param name="items"></param>
    /// <param name="index"></param>
    /// <returns></returns>
    protected string GetDateByIndex(IList<IShape> items, int index)
    {
      var empty = index <= 0 ? items.FirstOrDefault()?.X : items.LastOrDefault()?.X;
      var stamp = (long)(items.ElementAtOrDefault(index)?.X ?? empty ?? DateTime.Now.Ticks);

      return $"{new DateTime(stamp):HH:mm}";
    }

    protected virtual void CreateAccounts()
    {
      var account = new Account
      {
        Balance = 25000,
        States = new Map<string, SummaryModel>
        {
          //["SPY"] = new() { Instrument = new() { Name = "SPY" }},
          //["JEPQ"] = new() { Instrument = new() { Name = "JEPQ" }},
          ["ESU25"] = new() { Instrument = new() { Name = "ESU25", StepValue = 12.50, StepSize = 0.25, Leverage = 50, Commission = 3.65 } },
          ["NQU25"] = new() { Instrument = new() { Name = "NQU25", StepValue = 5, StepSize = 0.25, Leverage = 20 } },
          //["YMU25"] = new() { Instrument = new() { Name = "YMU25" } },
          //["ZBU25"] = new() { Instrument = new() { Name = "ZBU25" } },
          //["6EU25"] = new() { Instrument = new() { Name = "6EU25" } },
          //["6JU25"] = new() { Instrument = new() { Name = "6JU25" } },
          //["CLN25"] = new() { Instrument = new() { Name = "CLN25" } },
          //["GCQ25"] = new() { Instrument = new() { Name = "GCQ25" } },
          //["BTCU25"] = new() { Instrument = new() { Name = "BTCU25" } }
        },
      };

      View.Adapters["Prime"] = new Adapter
      {
        Speed = 1,
        Account = account,
        Source = "D:/Code/NET/Terminal/Data/FUTS/2025-06-17" // Configuration["Simulation:Source"]
      };

      Performance = new PerformanceIndicator { Name = "Balance" };
      Scales = account.States.ToDictionary(o => o.Key, o => new ScaleIndicator { Name = o.Key, Min = -1, Max = 1 });

      View
        .Adapters
        .Values
        .ForEach(adapter => adapter.Stream += message => OnData(message));
    }

    protected async void OnData(PointModel point)
    {
      var adapter = View.Adapters["Prime"];
      var account = adapter.Account;

      if (account.States.Values.Any(o => o.PointGroups.Count is 0))
      {
        return;
      }

      var performance = Performance.Update([account]);
      var scaleX = Scales["NQU25"].Update(account.States["NQU25"].PointGroups);
      var scaleY = Scales["ESU25"].Update(account.States["ESU25"].PointGroups);
      var priceX = account.States["NQU25"].PointGroups.Last();
      var priceY = account.States["ESU25"].PointGroups.Last();
      var spread = Math.Abs((scaleX.Point.Last - scaleY.Point.Last).Value);

      if (account.Orders.Count is 0 && account.Positions.Count is 0 && spread > 0.1 && spread < 1)
      {
        var isLong = scaleX.Point.Last > PreviousLeader.Last && scaleX.Point.Last > scaleY.Point.Last;
        var isShort = scaleX.Point.Last < PreviousLeader.Last && scaleX.Point.Last < scaleY.Point.Last;

        switch (true)
        {
          case true when isLong: await OpenPositions(priceY.Instrument, OrderSideEnum.Long); break;
          case true when isShort: await OpenPositions(priceY.Instrument, OrderSideEnum.Short); break;
        }
      }

      if (account.Positions.Count is not 0)
      {
        var pos = account.Positions.Values.First();
        var closeLong = pos.Side is OrderSideEnum.Long && scaleX.Point.Last < scaleY.Point.Last;
        var closeShort = pos.Side is OrderSideEnum.Short && scaleX.Point.Last > scaleY.Point.Last;

        if (closeLong || closeShort)
        {
          await ClosePositions();
        }
      }

      PreviousLeader = scaleX.Point.Clone() as PointModel;
      PreviousFollower = scaleY.Point.Clone() as PointModel;

      var com = new ComponentModel { Color = SKColors.LimeGreen };
      var comUp = new ComponentModel { Color = SKColors.DeepSkyBlue };
      var comDown = new ComponentModel { Color = SKColors.OrangeRed };

      DealsView.UpdateItems([.. View.Adapters.Values]);
      OrdersView.UpdateItems([.. View.Adapters.Values]);
      PositionsView.UpdateItems([.. View.Adapters.Values]);
      LeaderView.UpdateItems(point.Time.Value.Ticks, "Prices", "Leader", LeaderView.GetShape<CandleShape>(priceX));
      FollowerView.UpdateItems(point.Time.Value.Ticks, "Prices", "Spread", new AreaShape { Y = spread, Component = com });
      IndicatorsView.UpdateItems(point.Time.Value.Ticks, "Indicators", "X", new LineShape { Y = scaleX.Point.Last, Component = comUp });
      IndicatorsView.UpdateItems(point.Time.Value.Ticks, "Indicators", "Y", new LineShape { Y = scaleY.Point.Last, Component = comDown });
      PerformanceView.UpdateItems(point.Time.Value.Ticks, "Performance", "Balance", new AreaShape { Y = account.Balance });
      PerformanceView.UpdateItems(point.Time.Value.Ticks, "Performance", "PnL", PerformanceView.GetShape<LineShape>(performance.Point, SKColors.OrangeRed));
    }

    /// <summary>
    /// Open positions
    /// </summary>
    /// <param name="asset"></param>
    protected async Task OpenPositions(InstrumentModel asset, OrderSideEnum side)
    {
      var adapter = View.Adapters["Prime"];
      var order = new OrderModel
      {
        Amount = 1,
        Side = side,
        Type = OrderTypeEnum.Market,
        Transaction = new() { Instrument = asset }
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
            Amount = position.Transaction.Amount,
            Side = position.Side is OrderSideEnum.Long ? OrderSideEnum.Short : OrderSideEnum.Long,
            Type = OrderTypeEnum.Market,
            Transaction = new()
            {
              Instrument = position.Transaction.Instrument
            }
          };

          await adapter.SendOrder(order);
        }
      }
    }
  }
}
