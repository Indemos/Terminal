using Canvas.Core.Shapes;
using Core.Enums;
using Core.Indicators;
using Core.Models;
using Dashboard.Components;
using Estimator.Services;
using Simulation;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dashboard.Pages.Futures
{
  public partial class Covariance
  {
    public class Indexer : List<(long, double)>
    {
      public new void Add((long, double) item)
      {
        if (Count is 0 || item.Item1 > this[^1].Item1)
        {
          base.Add(item);
        }

        this[^1] = item;
      }
    }

    RatioService Ratio { get; set; }
    ChartsComponent DataView { get; set; }
    ChartsComponent IndicatorsView { get; set; }
    ChartsComponent PerformanceView { get; set; }
    TransactionsComponent TransactionsView { get; set; }
    OrdersComponent OrdersView { get; set; }
    PositionsComponent PositionsView { get; set; }
    StatementsComponent StatementsView { get; set; }
    PerformanceIndicator Performance { get; set; }
    Dictionary<string, ScaleIndicator> Scales { get; set; }

    int Direction { get; set; } = 0;
    Indexer Scores { get; set; } = new();
    Price PriceX { get; set; }
    Price PriceY { get; set; }

    const string nameX = "ES";
    const string nameY = "NQ";

    Dictionary<string, Instrument> Instruments = new()
    {
      [nameX] = new() { Name = nameX, Leverage = 50, Commission = 3.65, TimeFrame = TimeSpan.FromMinutes(1) },
      [nameY] = new() { Name = nameY, Leverage = 20, Commission = 3.65, TimeFrame = TimeSpan.FromMinutes(1) },
    };

    protected override async Task OnView()
    {
      await DataView.Create(nameof(DataView));
      await IndicatorsView.Create(nameof(IndicatorsView));
      await PerformanceView.Create(nameof(PerformanceView));

      DataView.Composers.ForEach(o => o.ShowIndex = i => GetDate(o.Items, (int)i));
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

      Ratio = new(100);
      Performance = new PerformanceIndicator();
      Scales = adapter.Account.Instruments.Keys.ToDictionary(o => o, name => new ScaleIndicator
      {
        Min = -1,
        Max = 1
      });

      return base.OnTrade();
    }

    protected async void Render(Instrument instrument, double spread)
    {
      var adapter = Adapter;
      var account = adapter.Account;
      var price = instrument.Price;
      var index = price.Bar.Time.Value;

      if (PriceX is null || PriceY is null)
      {
        return;
      }

      var performance = await Performance.Update([adapter]);
      var scaleX = Scales[nameX].Update(PriceX);
      var scaleY = Scales[nameY].Update(PriceY);

      OrdersView.Update(Adapters.Values);
      PositionsView.Update(Adapters.Values);
      TransactionsView.Update(Adapters.Values);
      DataView.Update(index, nameof(DataView), "Spread", new AreaShape { Y = spread, Component = ComUp });
      IndicatorsView.Update(index, nameof(IndicatorsView), "X", new LineShape { Y = scaleX, Component = ComUp });
      IndicatorsView.Update(index, nameof(IndicatorsView), "Y", new LineShape { Y = scaleY, Component = ComDown });
      PerformanceView.Update(index, nameof(PerformanceView), "Balance", new AreaShape { Y = account.Balance + account.Performance });
      PerformanceView.Update(index, nameof(PerformanceView), "PnL", PerformanceView.GetShape<LineShape>(performance, SKColors.OrangeRed));
    }

    protected override async Task OnTradeUpdate(Instrument instrument)
    {
      var price = instrument.Price;
      var adapter = Adapter;
      var account = adapter.Account;
      var assetX = account.Instruments[nameX];
      var assetY = account.Instruments[nameY];

      switch (instrument.Name)
      {
        case nameX: PriceX = price; break;
        case nameY: PriceY = price; break;
      }

      if (instrument.Name == "NQ") return;

      if (PriceX is null || PriceY is null)
      {
        return;
      }

      var orders = (await adapter.GetOrders(default)).Data;
      var positions = (await adapter.GetPositions(default)).Data;

      var inX = Math.Log(PriceX.Last.Value * assetX.Leverage.Value);
      var inY = Math.Log(PriceY.Last.Value * assetY.Leverage.Value);
      Ratio.Update(inX, inY);
      var spread = 100000 * Ratio.Spread(inX, inY);

      if (orders.Count is 0)
      {
        var isLong = spread < -5;
        var isShort = spread > 5;

        if (positions.Count is 0)
        {
          switch (true)
          {
            case true when isLong:
              Direction = 1;
              await OpenPosition(adapter, assetX, OrderSideEnum.Long);
              await OpenPosition(adapter, assetY, OrderSideEnum.Short);
              break;

            case true when isShort:
              Direction = -1;
              await OpenPosition(adapter, assetX, OrderSideEnum.Short);
              await OpenPosition(adapter, assetY, OrderSideEnum.Long);
              break;
          }
        }

        if (positions.Count is not 0)
        {
          var closeLong = Direction is 1 && spread > 0;
          var closeShort = Direction is -1 && spread < 0;

          if (closeLong || closeShort)
          {
            await ClosePosition(adapter);
          }
        }
      }

      Render(instrument, spread);
    }
  }
}
