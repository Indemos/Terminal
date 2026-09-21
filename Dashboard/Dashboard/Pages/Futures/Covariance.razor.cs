using Canvas.Core.Shapes;
using Core.Enums;
using Core.Indicators;
using Core.Models;
using Dashboard.Components;
using Estimator.Services;
using Simulation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dashboard.Pages.Futures
{
  public partial class Covariance
  {
    KalmanService Spread { get; set; }
    ChartsComponent ItemsView { get; set; }
    ChartsComponent ScoresView { get; set; }
    ChartsComponent IndicatorsView { get; set; }
    ChartsComponent PerformanceView { get; set; }
    TransactionsComponent TransactionsView { get; set; }
    OrdersComponent OrdersView { get; set; }
    PositionsComponent PositionsView { get; set; }
    StatementsComponent StatementsView { get; set; }
    PerformanceIndicator Performance { get; set; }
    VarianceIndicator Variance { get; set; }
    VarianceIndicator ScaleVariance { get; set; }
    Dictionary<string, ScaleIndicator> Scales { get; set; }

    int Direction { get; set; } = 0;
    Price PriceX { get; set; }
    Price PriceY { get; set; }

    const string nameX = "ES";
    const string nameY = "NQ";

    Dictionary<string, Instrument> Instruments = new()
    {
      [nameX] = new() { Name = nameX, Leverage = 50, Commission = 3.65 },
      [nameY] = new() { Name = nameY, Leverage = 20, Commission = 3.65 },
    };

    protected override async Task OnView()
    {
      await ItemsView.Create(nameof(ItemsView));
      await ScoresView.Create(nameof(ScoresView));
      await IndicatorsView.Create(nameof(IndicatorsView));
      await PerformanceView.Create(nameof(PerformanceView));

      ItemsView.Composers.ForEach(o => o.ShowIndex = i => GetDate(o.Items, (int)i));
      IndicatorsView.Composers.ForEach(o => o.ShowIndex = i => GetDate(o.Items, (int)i));
      PerformanceView.Composers.ForEach(o => o.ShowIndex = i => GetDate(o.Items, (int)i));
      ScoresView.Composers.ForEach(o => o.ShowIndex = i => GetDate(o.Items, (int)i));
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

      PriceX = null;
      PriceY = null;
      Variance = new();
      ScaleVariance = new();
      Spread = new(2, 0.00001, 0.01);
      Performance = new PerformanceIndicator();
      Scales = adapter.Account.Instruments.Keys.ToDictionary(o => o, name => new ScaleIndicator { Mode = ScaleMode.Pin, Period = 10000 });

      return base.OnTrade();
    }

    protected async void Render(
      Instrument instrument,
      double? spread,
      double? scaleX,
      double? scaleY,
      VarianceIndicator variance,
      VarianceIndicator scaleVariance)
    {
      if (PriceX is null || PriceY is null)
      {
        return;
      }

      var adapter = Adapter;
      var account = adapter.Account;
      var price = instrument.Price;
      var index = price.Time.Value;
      var scaleSpread = scaleX - scaleY;
      var performance = await Performance.Update([adapter]);

      OrdersView.Update(Adapters.Values);
      PositionsView.Update(Adapters.Values);
      TransactionsView.Update(Adapters.Values, new() { Count = 100 });
      PerformanceView.Update(index, nameof(PerformanceView), "Balance", new AreaShape { Y = account.Balance + account.Performance });
      PerformanceView.Update(index, nameof(PerformanceView), "PnL", new LineShape { Y = performance, Component = ComDown });

      ItemsView.Update(index, nameof(ItemsView), "Spread", new AreaShape { Y = spread, Component = Com });
      ItemsView.Update(index, nameof(ItemsView), "Spread Up", new LineShape { Y = variance.Deviation * 2, Component = ComUp });
      ItemsView.Update(index, nameof(ItemsView), "Spread Down", new LineShape { Y = -variance.Deviation * 2, Component = ComDown });

      ScoresView.Update(index, nameof(ScoresView), "Spread", new AreaShape { Y = scaleSpread, Component = ComUp });
      ScoresView.Update(index, nameof(ScoresView), "Spread Up", new LineShape { Y = scaleVariance.Deviation * 2, Component = ComUp });
      ScoresView.Update(index, nameof(ScoresView), "Spread Down", new LineShape { Y = -scaleVariance.Deviation * 2, Component = ComDown });

      IndicatorsView.Update(index, nameof(IndicatorsView), "X", new LineShape { Y = scaleX, Component = ComUp });
      IndicatorsView.Update(index, nameof(IndicatorsView), "Y", new LineShape { Y = scaleY, Component = ComDown });
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

      if (instrument.Name == nameY) return;

      if (PriceX is null || PriceY is null)
      {
        return;
      }

      var inX = Math.Log(PriceX.Last.Value);
      var inY = Math.Log(PriceY.Last.Value);
      var scaleX = 10000 * Scales[nameX].Update(PriceX).Value;
      var scaleY = 10000 * Scales[nameY].Update(PriceY).Value;
      var spread = Spread.Update(scaleX, 1, scaleY);
      var scaleSpread = scaleX - scaleY;

      if (spread is null)
      {
        return;
      }

      //var betas = Ratio.Betas();
      var variance = Variance.Update(spread.Value);
      var scaleVariance = ScaleVariance.Update(scaleSpread);
      var isLong = spread < -variance.Deviation * 2 && scaleSpread < -scaleVariance.Deviation;
      var isShort = spread > variance.Deviation * 2 && scaleSpread > scaleVariance.Deviation;
      var orders = (await adapter.GetOrders(default)).Data;
      var positions = (await adapter.GetPositions(default)).Data;

      if (orders.Count is 0)
      {
        if (positions.Count is 0)
        {
          switch (true)
          {
            case true when isLong:
              Direction = 1;
              await OpenPosition(adapter, assetX with { Price = PriceX }, OrderSideEnum.Long);
              await OpenPosition(adapter, assetY with { Price = PriceY }, OrderSideEnum.Short);
              break;

            case true when isShort:
              Direction = -1;
              await OpenPosition(adapter, assetX with { Price = PriceX }, OrderSideEnum.Short);
              await OpenPosition(adapter, assetY with { Price = PriceY }, OrderSideEnum.Long);
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

      Render(instrument, spread, scaleX, scaleY, variance, scaleVariance);
    }
  }
}
