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
using Terminal.Core.Extensions;
using Terminal.Core.Indicators;
using Terminal.Core.Models;
using Terminal.Services;

namespace Terminal.Pages.Shares
{
  public class DomData
  {
    public double? MaxBid;
    public double? MaxAsk;
    public double? CumBid;
    public double? CumAsk;
  }

  public partial class Dom
  {
    [Inject] IConfiguration Configuration { get; set; }

    protected virtual ControlsComponent View { get; set; }
    protected virtual ChartsComponent ChartsView { get; set; }
    protected virtual ChartsComponent OptionsView { get; set; }
    protected virtual ChartsComponent IndicatorsView { get; set; }
    protected virtual ChartsComponent PerformanceView { get; set; }
    protected virtual DealsComponent DealsView { get; set; }
    protected virtual OrdersComponent OrdersView { get; set; }
    protected virtual PositionsComponent PositionsView { get; set; }
    protected virtual StatementsComponent StatementsView { get; set; }
    protected virtual PerformanceIndicator Performance { get; set; }
    protected virtual MaIndicator AskLine { get; set; }
    protected virtual MaIndicator BidLine { get; set; }
    protected virtual Map<long?, DomData> Doms { get; set; }
    protected virtual ConcurrentGroup<PointModel> Bids { get; set; }
    protected virtual ConcurrentGroup<PointModel> Asks { get; set; }

    /// <summary>
    /// Setup views and adapters
    /// </summary>
    /// <param name="setup"></param>
    protected override async Task OnAfterRenderAsync(bool setup)
    {
      await CreateViews();

      if (setup)
      {
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
    /// Views
    /// </summary>
    protected virtual async Task CreateViews()
    {
      await ChartsView.Create("Prices");
      await OptionsView.Create("Options");
      await IndicatorsView.Create("Indicators");
      await PerformanceView.Create("Performance");

      ChartsView.Composers.ForEach(o => o.ShowIndex = i => GetDateByIndex(o.Items, (int)i));
      OptionsView.Composers.ForEach(o => o.ShowIndex = i => GetDateByIndex(o.Items, (int)i));
      IndicatorsView.Composers.ForEach(o => o.ShowIndex = i => GetDateByIndex(o.Items, (int)i));
      PerformanceView.Composers.ForEach(o => o.ShowIndex = i => GetDateByIndex(o.Items, (int)i));
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

    /// <summary>
    /// Accounts
    /// </summary>
    protected virtual void CreateAccounts()
    {
      var account = new Account
      {
        Balance = 25000,
        States = new Map<string, SummaryModel>
        {
          ["SPY"] = new SummaryModel { TimeFrame = TimeSpan.FromMinutes(1), Instrument = new InstrumentModel { Name = "SPY" } }
        }
      };

      View.Adapters["Prime"] = new Adapter
      {
        Speed = 1,
        Account = account,
        Source = Configuration["Simulation:Source"]
      };

      Bids = [];
      Asks = [];
      Doms = new Map<long?, DomData>();
      AskLine = new MaIndicator { Name = "AskLine", Interval = 50 };
      BidLine = new MaIndicator { Name = "BidLine", Interval = 50 };
      Performance = new PerformanceIndicator { Name = "Balance" };

      View
        .Adapters
        .Values
        .ForEach(adapter => adapter.Stream += async message => await OnData(message.Next));
    }

    /// <summary>
    /// Process tick data
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    protected async Task OnData(PointModel point)
    {
      var adapter = View.Adapters["Prime"];
      var account = adapter.Account;
      var performance = Performance.Update([account]);
      var summary = account.States.Get(point.Instrument.Name);
      var position = account.Positions.Values.FirstOrDefault(o => o.Transaction.Instrument.Derivative is null);
      var com = new ComponentModel { Color = SKColors.LimeGreen };
      var comUp = new ComponentModel { Color = SKColors.DeepSkyBlue };
      var comDown = new ComponentModel { Color = SKColors.OrangeRed };

      // Dom

      var countBids = summary.Dom.Bids.OrderBy(o => o.Last).Select((o, i) => new { Index = i + 1, o.Volume }).Sum(o => o.Volume.Value * o.Index);
      var countAsks = summary.Dom.Asks.OrderByDescending(o => o.Last).Select((o, i) => new { Index = i + 1, o.Volume }).Sum(o => o.Volume.Value * o.Index);
      var maxBid = Doms[point.Time?.Ticks].MaxBid = Math.Max(Doms[point.Time?.Ticks].MaxBid ?? 0, countBids);
      var maxAsk = Doms[point.Time?.Ticks].MaxAsk = Math.Max(Doms[point.Time?.Ticks].MaxAsk ?? 0, countAsks);
      var cumBid = Doms[point.Time?.Ticks].CumBid = (Doms[point.Time?.Ticks].CumBid ?? 0) + countBids;
      var cumAsk = Doms[point.Time?.Ticks].CumAsk = (Doms[point.Time?.Ticks].CumAsk ?? 0) + countAsks;

      Bids.Add(new PointModel { Time = point.Time, Last = countBids }, point.TimeFrame);
      Asks.Add(new PointModel { Time = point.Time, Last = countAsks }, point.TimeFrame);

      var bidLine = BidLine.Update(Bids);
      var askLine = AskLine.Update(Asks);
      var isLong = countBids > countAsks && countBids > bidLine.Point.Last;
      var isShort = countBids < countAsks && countAsks > askLine.Point.Last;

      // Option Dom

      var puts = summary
        .Options
        .Where(o => o.Derivative.Side is OptionSideEnum.Put)
        .Where(o => o.Derivative.Strike > point.Last - 10 && o.Derivative.Strike < point.Last + 10);

      var calls = summary
        .Options
        .Where(o => o.Derivative.Side is OptionSideEnum.Call)
        .Where(o => o.Derivative.Strike > point.Last - 10 && o.Derivative.Strike < point.Last + 10);

      var optionDomUps = calls.Sum(o => o.Point.Last);
      var optionDomDowns = puts.Sum(o => o.Point.Last);

      if (isLong || isShort)
      {
        var order = new OrderModel
        {
          Amount = 100,
          Type = OrderTypeEnum.Market,
          Side = isShort ? OrderSideEnum.Short : OrderSideEnum.Long,
          Transaction = new() { Instrument = point.Instrument }
        };

        //await ClosePositions(o => o.Transaction.Instrument.Derivative is null);
        //await adapter.SendOrder(order);
      }

      OptionsView.UpdateItems(point.Time.Value.Ticks, "Options", "Asks", new BarShape { Y = optionDomUps, Component = comDown });
      OptionsView.UpdateItems(point.Time.Value.Ticks, "Options", "Bids", new BarShape { Y = -optionDomDowns, Component = comUp });
      IndicatorsView.UpdateItems(point.Time.Value.Ticks, "Indicators", "Asks", new BarShape { Y = countAsks, Component = comDown });
      IndicatorsView.UpdateItems(point.Time.Value.Ticks, "Indicators", "Bids", new BarShape { Y = -countBids, Component = comUp });
      IndicatorsView.UpdateItems(point.Time.Value.Ticks, "Indicators", "Average Asks", new LineShape { Y = askLine.Point.Last, Component = comDown });
      IndicatorsView.UpdateItems(point.Time.Value.Ticks, "Indicators", "Average Bids", new LineShape { Y = -bidLine.Point.Last, Component = comUp });

      DealsView.UpdateItems([.. View.Adapters.Values]);
      OrdersView.UpdateItems([.. View.Adapters.Values]);
      PositionsView.UpdateItems([.. View.Adapters.Values]);
      ChartsView.UpdateItems(point.Time.Value.Ticks, "Prices", "Bars", new LineShape { Y = point.Last });
      PerformanceView.UpdateItems(point.Time.Value.Ticks, "Performance", "Balance", new AreaShape { Y = account.Balance });
      PerformanceView.UpdateItems(point.Time.Value.Ticks, "Performance", "PnL", PerformanceView.GetShape<LineShape>(performance.Point, SKColors.OrangeRed));
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
