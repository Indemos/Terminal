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
    protected virtual ChartsComponent TradeView { get; set; }
    protected virtual ChartsComponent VixView { get; set; }
    protected virtual ChartsComponent LongVixView { get; set; }
    protected virtual ChartsComponent DomView { get; set; }
    protected virtual ChartsComponent InverseDomView { get; set; }
    protected virtual ChartsComponent ExtraIndicatorsView { get; set; }
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
    protected virtual PointModel PreviousAsk { get; set; }
    protected virtual PointModel PreviousBid { get; set; }
    protected virtual PointModel Trade { get; set; }
    protected virtual int TradeCount { get; set; }

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
      await VixView.Create("Vix");
      await TradeView.Create("Trades");
      await ChartsView.Create("Prices");
      await LongVixView.Create("LongVix");
      await DomView.Create("Dom");
      await InverseDomView.Create("InverseDom");
      await PerformanceView.Create("Performance");

      VixView.Composers.ForEach(o => o.ShowIndex = i => GetDateByIndex(o.Items, (int)i));
      TradeView.Composers.ForEach(o => o.ShowIndex = i => GetDateByIndex(o.Items, (int)i));
      ChartsView.Composers.ForEach(o => o.ShowIndex = i => GetDateByIndex(o.Items, (int)i));
      LongVixView.Composers.ForEach(o => o.ShowIndex = i => GetDateByIndex(o.Items, (int)i));
      DomView.Composers.ForEach(o => o.ShowIndex = i => GetDateByIndex(o.Items, (int)i));
      InverseDomView.Composers.ForEach(o => o.ShowIndex = i => GetDateByIndex(o.Items, (int)i));
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
          ["SPY"] = new SummaryModel
          {
            TimeFrame = TimeSpan.FromMinutes(1),
            Instrument = new InstrumentModel { Name = "SPY" }
          }
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
      Trade = new();
      PreviousAsk = new();
      PreviousBid = new();
      Doms = new Map<long?, DomData>();
      AskLine = new MaIndicator { Name = "AskLine", Interval = 50 };
      BidLine = new MaIndicator { Name = "BidLine", Interval = 50 };
      Performance = new PerformanceIndicator { Name = "Balance" };

      View
        .Adapters
        .Values
        .ForEach(adapter => adapter.Stream += message => OnData(message.Next));
    }

    /// <summary>
    /// Process tick data
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    protected void OnData(PointModel point)
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

      var stamp = point.Time?.Ticks;

      var otmAsks = summary.Dom.Asks.Where(o => o.Last >= point.Last).OrderBy(o => o.Last);
      var otmBids = summary.Dom.Bids.Where(o => o.Last <= point.Last).OrderByDescending(o => o.Last);
      var sumOtmAsks = otmAsks.Select((o, i) => new { Index = i + 1, o.Volume }).Sum(o => o.Volume.Value * o.Index);
      var sumOtmBids = otmBids.Select((o, i) => new { Index = i + 1, o.Volume }).Sum(o => o.Volume.Value * o.Index);

      var atmAsks = summary.Dom.Asks.Where(o => o.Last >= point.Last).OrderByDescending(o => o.Last);
      var atmBids = summary.Dom.Bids.Where(o => o.Last <= point.Last).OrderBy(o => o.Last);
      var sumAtmAsks = atmAsks.Select((o, i) => new { Index = i + 1, o.Volume }).Sum(o => o.Volume.Value * o.Index);
      var sumAtmBids = atmBids.Select((o, i) => new { Index = i + 1, o.Volume }).Sum(o => o.Volume.Value * o.Index);

      Doms[stamp].MaxBid = Math.Max(Doms[stamp].MaxBid ?? 0, sumAtmBids);
      Doms[stamp].MaxAsk = Math.Max(Doms[stamp].MaxAsk ?? 0, sumAtmAsks);

      Bids.Add(new PointModel { Time = point.Time, Last = sumAtmBids }, point.TimeFrame);
      Asks.Add(new PointModel { Time = point.Time, Last = sumAtmAsks }, point.TimeFrame);

      var bidLine = BidLine.Update(Bids);
      var askLine = AskLine.Update(Asks);
      var isLong = sumAtmBids > sumAtmAsks && sumAtmBids > bidLine.Point.Last;
      var isShort = sumAtmBids < sumAtmAsks && sumAtmAsks > askLine.Point.Last;

      // Dom Imbalance

      //var currentAsk = otmAsks.First();
      //var currentBid = otmBids.First();

      //var isPriceUp = currentAsk.Last > PreviousAsk.Last || (currentAsk.Last >= PreviousAsk.Last && currentBid.Last > PreviousBid.Last);
      //var isPriceDown = currentBid.Last < PreviousBid.Last || (currentBid.Last <= PreviousBid.Last && currentAsk.Last < PreviousAsk.Last);

      //var isBuyPressure =
      //  (PreviousAsk.Last == currentAsk.Last && PreviousBid.Last == currentBid.Last) &&
      //  (currentAsk.Volume < currentBid.Volume);

      //var isSellPressure =
      //  (PreviousAsk.Last == currentAsk.Last && PreviousBid.Last == currentBid.Last) &&
      //  (currentAsk.Volume > currentBid.Volume);

      //switch (true)
      //{
      //  case true when isPriceUp || isBuyPressure: Trade.BidSize += Math.Abs((PreviousAsk.Volume - currentAsk.Volume).Value); break;
      //  case true when isPriceDown || isSellPressure: Trade.AskSize += Math.Abs((PreviousBid.Volume - currentBid.Volume).Value); break;
      //}

      //TradeCount++;

      //if (Equals(Trade.Time, point.Time) is false)
      //{
      //  TradeCount = 1;
      //  Trade.BidSize = 0;
      //  Trade.AskSize = 0;
      //  Trade.Time = point.Time;
      //}

      //PreviousAsk.Last = currentAsk.Last;
      //PreviousAsk.Volume = currentAsk.Volume;
      //PreviousBid.Last = currentBid.Last;
      //PreviousBid.Volume = currentBid.Volume;

      var isPriceUp = point.Ask > PreviousAsk.Last || (point.Ask >= PreviousAsk.Last && point.Bid > PreviousBid.Last);
      var isPriceDown = point.Bid < PreviousBid.Last || (point.Bid <= PreviousBid.Last && point.Bid < PreviousAsk.Last);

      var isBuyPressure =
        (PreviousAsk.Last == point.Ask && PreviousBid.Last == point.Bid) &&
        (point.AskSize < point.BidSize);

      var isSellPressure =
        (PreviousAsk.Last == point.Ask && PreviousBid.Last == point.Bid) &&
        (point.AskSize > point.BidSize);

      //switch (true)
      //{
      //  case true when isPriceUp: Trade.BidSize += PreviousAsk.Volume; break;
      //  case true when isPriceDown: Trade.AskSize += PreviousBid.Volume; break;
      //  case true when isBuyPressure: Trade.BidSize += Math.Abs((PreviousAsk.Volume - point.AskSize).Value); break;
      //  case true when isSellPressure: Trade.AskSize += Math.Abs((PreviousBid.Volume - point.BidSize).Value); break;
      //}

      var deltaTime = point.Time?.Ticks - Trade.Time?.Ticks;

      Trade.Last += ((point.BidSize - point.AskSize) / (point.BidSize + point.AskSize)) * deltaTime;
      Trade.Volume += deltaTime;
      TradeCount++;

      if (Equals(Trade.Time?.Ticks, point.Time?.Ticks) is false)
      {
        TradeCount = 1;
        Trade.Last = 0;
        Trade.Volume = 0;
        Trade.BidSize = 0;
        Trade.AskSize = 0;
        Trade.Time = point.Time;
      }

      TradeView.UpdateItems(point.Time.Value.Ticks, "Trades", "Longs", new AreaShape { Y = Trade.Last > 0 ? Trade.Last / Trade.Volume : 0, Component = comUp });
      TradeView.UpdateItems(point.Time.Value.Ticks, "Trades", "Shorts", new AreaShape { Y = Trade.Last < 0 ? -Trade.Last / Trade.Volume : 0, Component = comDown });

      PreviousAsk.Last = point.Ask;
      PreviousAsk.Volume = point.AskSize;
      PreviousBid.Last = point.Bid;
      PreviousBid.Volume = point.BidSize;

      // Options

      var atmPut = summary
        .Options
        .Where(o => Equals(o.Derivative.ExpirationDate?.Date, point.Time?.AddDays(31).Date))
        .Where(o => o.Derivative.Side is OptionSideEnum.Put)
        .FirstOrDefault(o => o.Derivative.Strike > point.Last);

      var atmCall = summary
        .Options
        .Where(o => Equals(o.Derivative.ExpirationDate?.Date, point.Time?.AddDays(31).Date))
        .Where(o => o.Derivative.Side is OptionSideEnum.Call)
        .LastOrDefault(o => o.Derivative.Strike < point.Last);

      var zeroPut = summary
        .Options
        .Where(o => Equals(o.Derivative.ExpirationDate?.Date, point.Time?.Date))
        .Where(o => o.Derivative.Side is OptionSideEnum.Put)
        .FirstOrDefault(o => o.Derivative.Strike > point.Last);

      var zeroCall = summary
        .Options
        .Where(o => Equals(o.Derivative.ExpirationDate?.Date, point.Time?.Date))
        .Where(o => o.Derivative.Side is OptionSideEnum.Call)
        .LastOrDefault(o => o.Derivative.Strike < point.Last);

      var vix = (zeroPut.Point.Last + zeroCall.Point.Last) / point.Last;
      var vix30 = (atmPut.Point.Last + atmCall.Point.Last) / (point.Last * Math.Sqrt(30 / (2 * Math.PI * 365)));
      var sigma = summary.Options.Sum(o => o.Derivative.Variance.Vega);

      var gammaPuts = summary
        .Options
        .Where(o => o.Derivative.Side is OptionSideEnum.Put)
        .Where(o => o.Derivative.Strike > point.Last - 5)
        .Where(o => o.Derivative.Strike < point.Last + 5)
        .Sum(o => o.Derivative.Variance.Gamma);

      var gammaCalls = summary
        .Options
        .Where(o => o.Derivative.Side is OptionSideEnum.Call)
        .Where(o => o.Derivative.Strike > point.Last - 5)
        .Where(o => o.Derivative.Strike < point.Last + 5)
        .Sum(o => o.Derivative.Variance.Gamma);

      VixView.UpdateItems(point.Time.Value.Ticks, "Vix", "Vix + 0", new AreaShape { Y = vix * 100, Component = com });
      LongVixView.UpdateItems(point.Time.Value.Ticks, "LongVix", "Vix + 30", new AreaShape { Y = vix30 * 100, Component = com });
      DomView.UpdateItems(point.Time.Value.Ticks, "Dom", "Asks", new BarShape { Y = sumAtmAsks, Component = comDown });
      DomView.UpdateItems(point.Time.Value.Ticks, "Dom", "Bids", new BarShape { Y = -sumAtmBids, Component = comUp });
      DomView.UpdateItems(point.Time.Value.Ticks, "Dom", "Average Asks", new LineShape { Y = askLine.Point.Last, Component = com });
      DomView.UpdateItems(point.Time.Value.Ticks, "Dom", "Average Bids", new LineShape { Y = -bidLine.Point.Last, Component = com });

      InverseDomView.UpdateItems(point.Time.Value.Ticks, "InverseDom", "Asks", new AreaShape { Y = sumOtmAsks, Component = comDown });
      InverseDomView.UpdateItems(point.Time.Value.Ticks, "InverseDom", "Bids", new AreaShape { Y = -sumOtmBids, Component = comUp });

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
