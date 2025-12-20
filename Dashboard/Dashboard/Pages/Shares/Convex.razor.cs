using Canvas.Core.Extensions;
using Canvas.Core.Shapes;
using Core.Enums;
using Core.Indicators;
using Core.Models;
using Dashboard.Components;
using Simulation;
using SkiaSharp;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dashboard.Pages.Shares
{
  public partial class Convex
  {
    ControlsComponent View { get; set; }
    ChartsComponent DataView { get; set; }
    ChartsComponent PerformanceView { get; set; }
    OrdersComponent OrdersView { get; set; }
    PositionsComponent PositionsView { get; set; }
    StatementsComponent StatementsView { get; set; }
    TransactionsComponent TransactionsView { get; set; }
    PerformanceIndicator Performance { get; set; }

    double Step { get; set; } = 5;
    const string AssetX = "GOOG";
    const string AssetY = "GOOGL";

    Dictionary<string, double?> Prices = new()
    {
      [AssetX] = null,
      [AssetY] = null
    };

    Dictionary<string, Instrument> Instruments => new()
    {
      [AssetX] = new Instrument { Name = AssetX },
      [AssetY] = new Instrument { Name = AssetY }
    };

    protected override async Task OnView()
    {
      await DataView.Create("Prices");
      await PerformanceView.Create("Performance");
    }

    protected override Task OnTrade()
    {
      Performance = new PerformanceIndicator();
      Adapter = new SimGateway
      {
        Connector = Connector,
        Source = Configuration["Documents:Resources"],
        Account = new Account
        {
          Descriptor = "Demo",
          Balance = 25000,
          Instruments = Instruments
        }
      };

      return base.OnTrade();
    }

    protected override async Task OnViewUpdate(Instrument instrument)
    {
      if (instrument.Name == AssetY)
      {
        return;
      }

      var price = instrument.Price;
      var account = Adapter.Account;
      var performance = await Performance.Update(Adapters.Values);

      OrdersView.Update(Adapters.Values);
      PositionsView.Update(Adapters.Values);
      TransactionsView.Update(Adapters.Values);
      DataView.Update(price.Time.Value, "Prices", "Spread", new AreaShape { Y = price.Last, Component = Com });
      PerformanceView.Update(price.Time.Value, "Performance", "Balance", new AreaShape { Y = account.Balance + account.Performance });
      PerformanceView.Update(price.Time.Value, "Performance", "PnL", PerformanceView.GetShape<LineShape>(performance.Response, SKColors.OrangeRed));
    }

    protected override async Task OnTradeUpdate(Instrument instrument)
    {
      if (instrument.Name == AssetY)
      {
        return;
      }

      var account = Adapter.Account;
      var instrumentX = account.Instruments[AssetX];
      var instrumentY = account.Instruments[AssetY];
      var seriesX = (await Adapter.GetPrices(new() { Count = 1, Instrument = instrumentX })).Data;
      var seriesY = (await Adapter.GetPrices(new() { Count = 1, Instrument = instrumentY })).Data;
      var orders = (await Adapter.GetOrders(default)).Data;
      var positions = (await Adapter.GetPositions(default)).Data;

      if (orders.Count is not 0 || seriesX.Count is 0 || seriesY.Count is 0)
      {
        return;
      }

      var priceX = seriesX.Last();
      var priceY = seriesY.Last();

      await Trade(instrumentX with { Price = priceX }, OrderSideEnum.Long, positions);
      await Trade(instrumentY with { Price = priceY }, OrderSideEnum.Short, positions);
    }

    protected async Task Trade(Instrument instrument, OrderSideEnum direction, IList<Order> positions)
    {
      var price = instrument.Price;
      var account = Adapter.Account;

      Prices[instrument.Name] = Prices.Get(instrument.Name) ?? price.Last;

      if (positions.Count is not 0)
      {
        var isStepUp = price.Last - Prices[instrument.Name] > Step;
        var isStepDown = Prices[instrument.Name] - price.Last > Step;

        if (isStepUp)
        {
          Prices[instrument.Name] = price.Last;

          switch (direction)
          {
            case OrderSideEnum.Long: await OpenPosition(Adapter, instrument, OrderSideEnum.Long); break;
            case OrderSideEnum.Short: await ClosePosition(Adapter, o => o.Operation.Instrument.Name == instrument.Name); break;
          }
        }

        if (isStepDown)
        {
          Prices[instrument.Name] = price.Last;

          switch (direction)
          {
            case OrderSideEnum.Short: await OpenPosition(Adapter, instrument, OrderSideEnum.Short); break;
            case OrderSideEnum.Long: await ClosePosition(Adapter, o => o.Operation.Instrument.Name == instrument.Name); break;
          }
        }
      }

      if (positions.Count is 0)
      {
        Prices[instrument.Name] = price.Last;
        await OpenPosition(Adapter, instrument, direction);
      }
    }
  }
}
