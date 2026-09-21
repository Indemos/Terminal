using Canvas.Core.Shapes;
using Core.Enums;
using Core.Extensions;
using Core.Indicators;
using Core.Models;
using Core.Services;
using Dashboard.Components;
using Microsoft.AspNetCore.Components;
using Simulation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dashboard.Pages.Futures
{
  public partial class Orders
  {
    [Inject] StateService Subscription { get; set; }

    ChartsComponent DataView { get; set; }
    ChartsComponent ScoreView { get; set; }
    ChartsComponent IndicatorsView { get; set; }
    ChartsComponent PerformanceView { get; set; }
    TransactionsComponent TransactionsView { get; set; }
    OrdersComponent OrdersView { get; set; }
    PositionsComponent PositionsView { get; set; }
    StatementsComponent StatementsView { get; set; }
    PerformanceIndicator Performance { get; set; }
    DepthOfiCalculator OFI { get; set; }
    ImbalanceIndicator Imbalance { get; set; }
    VolumeDeltaIndicator Cvd { get; set; }

    int PosDirection { get; set; }
    int Direction { get; set; }

    double? prevOrderPrice { get; set; }
    double? prevPriceSize { get; set; }
    Dom prevDom { get; set; }

    const string symName = "ES";

    Dictionary<string, Instrument> Instruments = new()
    {
      [symName] = new() { Name = symName, Leverage = 50, Commission = 3.65, StepSize = 0.25 },
    };

    protected override async Task OnView()
    {
      await DataView.Create(nameof(DataView));
      await ScoreView.Create(nameof(ScoreView));
      await IndicatorsView.Create(nameof(IndicatorsView));
      await PerformanceView.Create(nameof(PerformanceView));

      DataView.Composers.ForEach(o => o.ShowIndex = i => GetDate(o.Items, (int)i));
      ScoreView.Composers.ForEach(o => o.ShowIndex = i => GetDate(o.Items, (int)i));
      IndicatorsView.Composers.ForEach(o => o.ShowIndex = i => GetDate(o.Items, (int)i));
      PerformanceView.Composers.ForEach(o => o.ShowIndex = i => GetDate(o.Items, (int)i));
    }

    protected override Task OnTrade()
    {
      var adapter = Adapter = new SimGateway
      {
        Connector = Connector,
        Source = Configuration["Documents:Resources"] + "/FUTS",
        Account = new()
        {
          Descriptor = "Demo",
          Balance = 25000,
          Instruments = Instruments
        }
      };

      Cvd = new();
      Imbalance = new();
      Performance = new();
      OFI = new();

      return base.OnTrade();
    }

    protected async void Render(DomOrder order)
    {
      var adapter = Adapter;
      var account = adapter.Account;
      var price = order.Price;
      var index = order.Time.Value;
      var performance = await Performance.Update([adapter]);
      var dom = await adapter.GetDom(new() { Instrument = Instruments[symName] });
      var (imbalance, signal, microprice) = OFI.Update(dom.Data);
      var domValue = Imbalance.Update(dom.Data, 1000);
      var cvdValue = Cvd.Update(order);

      OrdersView.Update(Adapters.Values);
      PositionsView.Update(Adapters.Values);
      TransactionsView.Update(Adapters.Values);
      DataView.Update(index, nameof(DataView), "Price", new LineShape { Y = price, Component = Com });
      PerformanceView.Update(index, nameof(PerformanceView), "Balance", new AreaShape { Y = account.Balance + account.Performance });
      PerformanceView.Update(index, nameof(PerformanceView), "PnL", new LineShape { Y = performance, Component = ComDown });
      IndicatorsView.Update(index, nameof(IndicatorsView), "Indicators", new LineShape { Y = domValue, Component = Com });
      ScoreView.Update(index, nameof(ScoreView), "CVD", new LineShape { Y = cvdValue, Component = Com });
    }

    protected double? Size(Dom dom, DomOrder order)
    {
      var orderPrice = (int)Math.Round((order.Price.Value / Instruments[symName].StepSize).Value);

      switch (order.Side)
      {
        case DomSide.Bid: return dom.Bids.Get(orderPrice)?.Sum(o => o.Size) ?? 0;
        case DomSide.Ask: return dom.Asks.Get(orderPrice)?.Sum(o => o.Size) ?? 0;
      }

      return 0;
    }

    protected override async Task OnDomUpdate(DomOrder order)
    {
      if (order.Action is DomAction.Trade is false) return;

      var dom = await Adapter.GetDom(new() { Instrument = Instruments[symName] });

      if (prevDom is not null)
      {
        var index = order.Time.Value; // .Round(TimeSpan.FromMinutes(1)).Value;
        var orderPrice = (int)Math.Round((order.Price.Value / Instruments[symName].StepSize).Value);
        var prevPriceSize = Size(prevDom, order);
        var priceSize = Size(dom.Data, order);

        // Evaluate Bid side (Aggressive buyers)
        if (order.Side is DomSide.Bid)
        {
        }

        // Evaluate Ask side (Aggressive Sellers)
        if (order.Side is DomSide.Ask)
        {
        }
      }

      prevOrderPrice = order.Price;
      prevPriceSize = order.Size;
      prevDom = dom.Data;

      var isLong = false;
      var isShort = false;
      var adapter = Adapter;
      var orders = (await adapter.GetOrders(default)).Data;
      var positions = (await adapter.GetPositions(default)).Data;

      if (positions.Count is 0)
      {
        switch (true)
        {
          case true when isLong:
            PosDirection = 1;
            await OpenPosition(adapter, Instruments[symName], OrderSideEnum.Long);
            break;

          case true when isShort:
            PosDirection = -1;
            await OpenPosition(adapter, Instruments[symName], OrderSideEnum.Short);
            break;
        }
      }

      if (positions.Count is not 0)
      {
        var closeLong = PosDirection > 0 && isShort;
        var closeShort = PosDirection < 0 && isLong;

        if (closeLong || closeShort)
        {
          await ClosePosition(adapter);
        }
      }

      Render(order);
    }
  }
}

/// <summary>
/// Event-driven Order Flow Imbalance (OFI), per Cont, Kukanov & Stoikov (2014),
/// extended to multiple depth levels per Xu, Gould & Howison (2018).
///
/// Create ONE instance per instrument and call Update() on a fixed cadence
/// (not on every raw DOM message — see note at the bottom).
/// </summary>
public sealed class DepthOfiCalculator
{
  private double _prevBestBidPrice, _prevBestBidSize;
  private double _prevBestAskPrice, _prevBestAskSize;
  private bool _hasPrev = false;

  private double _emaFast = 0, _emaSlow = 0;
  private const double AlphaFast = 0.15; // ~20 ticks
  private const double AlphaSlow = 0.02; // ~200 ticks

  public (double imbalance, double signal, double microprice) Update(Dom dom, int nLevels = 10, double decay = 10.0)
  {
    if (dom == null || dom.Bids.Count == 0 || dom.Asks.Count == 0)
      return (0, 0, 0);

    double bestBid = dom.Bids.Keys.Max();
    double bestAsk = dom.Asks.Keys.Min();
    double mid = (bestBid + bestAsk) * 0.5;
    double spread = Math.Max(bestAsk - bestBid, 1e-9);

    double wb = 0, wa = 0;
    var bidLevels = dom.Bids.OrderByDescending(k => k.Key).Take(nLevels).ToList();
    var askLevels = dom.Asks.OrderBy(k => k.Key).Take(nLevels).ToList();

    double medianSize = GetMedianSize(bidLevels.Concat(askLevels).ToList());

    foreach (var kv in bidLevels)
    {
      double size = kv.Value.Sum(o => o.Size ?? 0);
      size = Math.Min(size, medianSize * 5.0); // anti-spoof cap
      size = Math.Log(1.0 + size); // FIX: was Log1p
      double dist = Math.Abs(kv.Key - mid) / spread;
      double w = Math.Exp(-decay * dist);
      wb += size * kv.Key * w;
    }
    foreach (var kv in askLevels)
    {
      double size = kv.Value.Sum(o => o.Size ?? 0);
      size = Math.Min(size, medianSize * 5.0);
      size = Math.Log(1.0 + size); // FIX: was Log1p
      double dist = Math.Abs(kv.Key - mid) / spread;
      double w = Math.Exp(-decay * dist);
      wa += size * kv.Key * w;
    }

    double normImbalance = (wb - wa) / (wb + wa + 1e-9);
    double microprice = (bestAsk * wb + bestBid * wa) / (wb + wa + 1e-9) - mid;

    double ofi = 0;
    double currBestBidSize = bidLevels.First().Value.Sum(o => o.Size ?? 0);
    double currBestAskSize = askLevels.First().Value.Sum(o => o.Size ?? 0);

    if (_hasPrev)
    {
      // Bid OFI
      if (bestBid > _prevBestBidPrice) ofi += currBestBidSize;
      else if (bestBid == _prevBestBidPrice) ofi += currBestBidSize - _prevBestBidSize;
      else ofi -= _prevBestBidSize;

      // Ask OFI (inverted)
      if (bestAsk < _prevBestAskPrice) ofi -= currBestAskSize;
      else if (bestAsk == _prevBestAskPrice) ofi -= currBestAskSize - _prevBestAskSize;
      else ofi += _prevBestAskSize;

      double depth = currBestBidSize + currBestAskSize + 1e-9;
      ofi = ofi / depth;
      ofi = ofi < -1 ? -1 : ofi > 1 ? 1 : ofi; // Clamp
    }

    _prevBestBidPrice = bestBid;
    _prevBestBidSize = currBestBidSize;
    _prevBestAskPrice = bestAsk;
    _prevBestAskSize = currBestAskSize;
    _hasPrev = true;

    double rawPressure = 0.6 * normImbalance + 0.4 * ofi;
    _emaFast = _emaFast * (1 - AlphaFast) + rawPressure * AlphaFast;
    _emaSlow = _emaSlow * (1 - AlphaSlow) + rawPressure * AlphaSlow;
    double signal = _emaFast - _emaSlow;

    return (normImbalance, signal, microprice / spread);
  }

  private double GetMedianSize(IList<KeyValuePair<long, LinkedList<DomOrder>>> levels)
  {
    var sizes = levels.Select(kv => kv.Value.Sum(o => o.Size ?? 0)).OrderBy(s => s).ToList();
    if (sizes.Count == 0) return 1;
    return sizes[sizes.Count / 2];
  }
}
