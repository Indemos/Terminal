using Canvas.Core.Shapes;
using Core.Enums;
using Core.Indicators;
using Core.Models;
using Dashboard.Components;
using Simulation;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dashboard.Pages.Futures
{
  public partial class Leads
  {
    ControlsComponent View { get; set; }
    ChartsComponent DataView { get; set; }
    ChartsComponent SpreadView { get; set; }
    ChartsComponent IndicatorsView { get; set; }
    ChartsComponent PerformanceView { get; set; }
    TransactionsComponent TransactionsView { get; set; }
    OrdersComponent OrdersView { get; set; }
    PositionsComponent PositionsView { get; set; }
    StatementsComponent StatementsView { get; set; }
    PerformanceIndicator Performance { get; set; }
    Dictionary<string, ScaleIndicator> Scales { get; set; }

    double ActionableSpread { get; set; } = 0.1;
    Price PreviousLeader { get; set; }
    Price PreviousFollower { get; set; }

    Dictionary<string, Instrument> Instruments = new()
    {
      ["ESU25"] = new() { Name = "ESU25", StepValue = 12.50, StepSize = 0.25, Leverage = 50, Commission = 3.65 },
      ["NQU25"] = new() { Name = "NQU25", StepValue = 5, StepSize = 0.25, Leverage = 20, Commission = 3.65 },
    };

    protected override async Task OnView()
    {
      await DataView.Create(nameof(DataView));
      await SpreadView.Create(nameof(SpreadView));
      await IndicatorsView.Create(nameof(IndicatorsView));
      await PerformanceView.Create(nameof(PerformanceView));

      DataView.Composers.ForEach(o => o.ShowIndex = i => GetDate(o.Items, (int)i));
      SpreadView.Composers.ForEach(o => o.ShowIndex = i => GetDate(o.Items, (int)i));
      IndicatorsView.Composers.ForEach(o => o.ShowIndex = i => GetDate(o.Items, (int)i));
      PerformanceView.Composers.ForEach(o => o.ShowIndex = i => GetDate(o.Items, (int)i));
    }

    protected override Task OnTrade()
    {
      var adapter = Adapter = new SimGateway
      {
        Connector = Connector,
        Source = Configuration["Documents:Resources"] + "/FUTS/2025-06-17",
        Account = new()
        {
          Descriptor = "Demo",
          Balance = 25000,
          Instruments = Instruments
        }
      };

      Performance = new PerformanceIndicator { Name = "Balance" };
      Scales = adapter.Account.Instruments.Keys.ToDictionary(o => o, name => new ScaleIndicator
      {
        Name = name,
        Min = -1,
        Max = 1
      });

      return base.OnTrade();
    }

    protected override async Task OnViewUpdate(Instrument instrument)
    {
      var price = instrument.Price;
      var adapter = Adapter;
      var account = adapter.Account;
      var assetX = account.Instruments["ESU25"];
      var assetY = account.Instruments["NQU25"];
      var seriesX = (await adapter.GetPrices(new() { Count = 1, Instrument = assetX })).Data;
      var seriesY = (await adapter.GetPrices(new() { Count = 1, Instrument = assetY })).Data;

      if (seriesX.Count is 0 || seriesY.Count is 0)
      {
        return;
      }

      var performance = await Performance.Update([adapter]);
      var scaleX = await Scales[assetX.Name].Update(seriesX);
      var scaleY = await Scales[assetY.Name].Update(seriesY);
      var priceX = seriesX.Last();
      var priceY = seriesY.Last();
      var spread = Math.Abs((scaleX.Response.Last - scaleY.Response.Last).Value);

      OrdersView.Update(Adapters.Values);
      PositionsView.Update(Adapters.Values);
      TransactionsView.Update(Adapters.Values);
      DataView.Update(price.Time.Value, nameof(DataView), "Leader", new AreaShape { Y = priceX.Last, Component = ComUp });
      SpreadView.Update(price.Time.Value, nameof(SpreadView), "Spread", new AreaShape { Y = spread, Component = Com });
      IndicatorsView.Update(price.Time.Value, nameof(IndicatorsView), "X", new LineShape { Y = scaleX.Response.Last, Component = ComUp });
      IndicatorsView.Update(price.Time.Value, nameof(IndicatorsView), "Y", new LineShape { Y = scaleY.Response.Last, Component = ComDown });
      PerformanceView.Update(price.Time.Value, nameof(PerformanceView), "Balance", new AreaShape { Y = account.Balance + account.Performance });
      PerformanceView.Update(price.Time.Value, nameof(PerformanceView), "PnL", PerformanceView.GetShape<LineShape>(performance.Response, SKColors.OrangeRed));
    }

    protected override async Task OnTradeUpdate(Instrument instrument)
    {
      if (Equals(instrument.Name, "ESU25") is false)
      {
        return;
      }

      var price = instrument.Price;
      var adapter = Adapter;
      var account = adapter.Account;
      var assetX = account.Instruments["ESU25"];
      var assetY = account.Instruments["NQU25"];
      var seriesX = (await adapter.GetPrices(new Criteria { Count = 1, Instrument = assetX })).Data;
      var seriesY = (await adapter.GetPrices(new Criteria { Count = 1, Instrument = assetY })).Data;

      if (seriesX.Count is 0 || seriesY.Count is 0)
      {
        return;
      }

      var orders = (await adapter.GetOrders(default)).Data;
      var positions = (await adapter.GetPositions(default)).Data;
      var performance = await Performance.Update([adapter]);
      var scaleX = await Scales[assetX.Name].Update(seriesX);
      var scaleY = await Scales[assetY.Name].Update(seriesY);
      var priceX = seriesX.Last();
      var priceY = seriesY.Last();
      var spread = Math.Abs((scaleX.Response.Last - scaleY.Response.Last).Value);

      if (orders.Count is 0)
      {
        if (PreviousLeader is not null && positions.Count is 0 && spread > ActionableSpread)
        {
          var isLong = scaleX.Response.Last > PreviousLeader.Last && scaleX.Response.Last > scaleY.Response.Last;
          var isShort = scaleX.Response.Last < PreviousLeader.Last && scaleX.Response.Last < scaleY.Response.Last;

          switch (true)
          {
            case true when isLong: await OpenPosition(adapter, assetY, OrderSideEnum.Long); break;
            case true when isShort: await OpenPosition(adapter, assetY, OrderSideEnum.Short); break;
          }
        }

        if (positions.Count is not 0)
        {
          var pos = positions.First();
          var closeLong = pos.Side is OrderSideEnum.Long && scaleX.Response.Last < scaleY.Response.Last;
          var closeShort = pos.Side is OrderSideEnum.Short && scaleX.Response.Last > scaleY.Response.Last;

          if (closeLong || closeShort)
          {
            await ClosePosition(adapter);
          }
        }
      }

      PreviousLeader = scaleX.Response with { };
      PreviousFollower = scaleY.Response with { };
    }
  }
}
