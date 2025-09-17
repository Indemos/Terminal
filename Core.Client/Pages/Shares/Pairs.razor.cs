using Canvas.Core.Models;
using Canvas.Core.Shapes;
using Core.Client.Components;
using Core.Client.Services;
using Core.Common.Enums;
using Core.Common.Indicators;
using Core.Common.States;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Orleans;
using Orleans.Streams;
using Simulation;
using SkiaSharp;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Client.Pages.Shares
{
  public partial class Pairs
  {
    [Inject] IClusterClient Connector { get; set; }
    [Inject] IConfiguration Configuration { get; set; }
    [Inject] SubscriptionService Observer { get; set; }

    /// <summary>
    /// Strategy
    /// </summary>
    const string assetX = "GOOG";
    const string assetY = "GOOGL";

    protected virtual ControlsComponent View { get; set; }
    protected virtual ChartsComponent ChartsView { get; set; }
    protected virtual ChartsComponent PerformanceView { get; set; }
    protected virtual TransactionsComponent TransactionsView { get; set; }
    protected virtual OrdersComponent OrdersView { get; set; }
    protected virtual PositionsComponent PositionsView { get; set; }
    protected virtual StatementsComponent StatementsView { get; set; }
    protected virtual PerformanceIndicator Performance { get; set; }

    /// <summary>
    /// Setup
    /// </summary>
    /// <param name="setup"></param>
    protected override async Task OnAfterRenderAsync(bool setup)
    {
      if (setup)
      {
        await ChartsView.Create("Prices");
        await PerformanceView.Create("Performance");

        Observer.OnMessageAsync += async state =>
        {
          switch (true)
          {
            case true when state.Previous is SubscriptionEnum.None && state.Next is SubscriptionEnum.Progress: await CreateAccounts(); break;
            case true when state.Previous is SubscriptionEnum.Progress && state.Next is SubscriptionEnum.Stream:

              await TransactionsView.UpdateItems([.. View.Adapters.Values]);
              await OrdersView.UpdateItems([.. View.Adapters.Values]);
              await PositionsView.UpdateItems([.. View.Adapters.Values]);

              break;
          }
        };
      }

      await base.OnAfterRenderAsync(setup);
    }

    /// <summary>
    /// Accounts
    /// </summary>
    protected virtual async Task CreateAccounts()
    {
      Performance = new PerformanceIndicator { Name = "Balance" };

      var adapter = View.Adapters["Prime"] = new SimGateway
      {
        Speed = 1,
        Connector = Connector,
        Space = $"{Guid.NewGuid()}",
        Source = Configuration["Simulation:Source"],
        Account = new AccountState
        {
          Descriptor = "Demo",
          Balance = 25000,
          Instruments = new()
          {
            [assetX] = new InstrumentState { Name = assetX },
            [assetY] = new InstrumentState { Name = assetY }
          }
        }
      };

      await adapter.Stream.SubscribeAsync((o, x) => OnPrice(o));
    }

    /// <summary>
    /// Stream
    /// </summary>
    /// <param name="price"></param>
    public virtual async Task OnPrice(PriceState price)
    {
      var adapter = View.Adapters["Prime"];
      var account = adapter.Account;
      var instrumentX = account.Instruments[assetX];
      var instrumentY = account.Instruments[assetY];
      var seriesX = (await adapter.Ticks(new MetaState { Count = 1, Instrument = instrumentX })).Data;
      var seriesY = (await adapter.Ticks(new MetaState { Count = 1, Instrument = instrumentY })).Data;

      if (seriesX.Count is 0 || seriesY.Count is 0)
      {
        return;
      }

      var orders = (await adapter.Orders(default)).Data;
      var positions = (await adapter.Positions(default)).Data;
      var performance = await Performance.Update([adapter]);
      var xPoint = seriesX.Last();
      var yPoint = seriesY.Last();
      var spread = (xPoint.Ask - xPoint.Bid) + (yPoint.Ask - yPoint.Bid);
      var expenses = spread;
      var posAmount = positions.Sum(o => o.Operation.Amount);

      if (orders.Count is 0)
      {
        var buy = positions.FirstOrDefault(o => o.Side is OrderSideEnum.Long);
        var sell = positions.FirstOrDefault(o => o.Side is OrderSideEnum.Short);

        if (buy is not null && sell is not null)
        {
          var gain = buy.Balance.Current + sell.Balance.Current;

          switch (true)
          {
            case true when gain > expenses * 2: await ClosePositions(); break;
            case true when gain < -expenses * posAmount: await OpenPositions(buy.Operation.Instrument, sell.Operation.Instrument); break;
          }
        }

        if (positions.Count is 0)
        {
          switch (true)
          {
            case true when (xPoint.Bid - yPoint.Ask) > expenses: await OpenPositions(instrumentY, instrumentX); break;
            case true when (yPoint.Bid - xPoint.Ask) > expenses: await OpenPositions(instrumentX, instrumentY); break;
          }
        }
      }

      var range = Math.Max(
        (xPoint.Bid - yPoint.Ask - expenses).Value,
        (yPoint.Bid - xPoint.Ask - expenses).Value);

      var com = new ComponentModel { Color = range > 0 ? SKColors.DeepSkyBlue : SKColors.OrangeRed };
      var comUp = new ComponentModel { Color = SKColors.DeepSkyBlue };
      var comDown = new ComponentModel { Color = SKColors.OrangeRed };

      await TransactionsView.UpdateItems([.. View.Adapters.Values]);
      await OrdersView.UpdateItems([.. View.Adapters.Values]);
      await PositionsView.UpdateItems([.. View.Adapters.Values]);
      await ChartsView.UpdateItems(price.Time.Value, "Prices", "Spread", new AreaShape { Y = range, Component = com });
      await PerformanceView.UpdateItems(price.Time.Value, "Performance", "Balance", new AreaShape { Y = account.Balance + account.Performance });
      await PerformanceView.UpdateItems(price.Time.Value, "Performance", "PnL", PerformanceView.GetShape<LineShape>(performance.Response, SKColors.OrangeRed));
    }

    /// <summary>
    /// Open positions
    /// </summary>
    /// <param name="assetBuy"></param>
    /// <param name="assetSell"></param>
    protected async Task OpenPositions(InstrumentState assetBuy, InstrumentState assetSell)
    {
      var adapter = View.Adapters["Prime"];
      var orderSell = new OrderState
      {
        Amount = 1,
        Side = OrderSideEnum.Short,
        Type = OrderTypeEnum.Market,
        Operation = new() { Instrument = assetSell }
      };

      var orderBuy = new OrderState
      {
        Amount = 1,
        Side = OrderSideEnum.Long,
        Type = OrderTypeEnum.Market,
        Operation = new() { Instrument = assetBuy }
      };

      await adapter.SendOrder(orderBuy);
      await adapter.SendOrder(orderSell);
    }

    /// <summary>
    /// Close positions
    /// </summary>
    /// <param name="condition"></param>
    public virtual async Task ClosePositions(Func<OrderState, bool> condition = null)
    {
      var adapter = View.Adapters["Prime"];
      var positions = (await adapter.Positions(default)).Data;
      var account = adapter.Account;

      foreach (var position in positions)
      {
        if (condition is null || condition(position))
        {
          var order = new OrderState
          {
            Amount = position.Operation.Amount,
            Side = position.Side is OrderSideEnum.Long ? OrderSideEnum.Short : OrderSideEnum.Long,
            Type = OrderTypeEnum.Market,
            Operation = new()
            {
              Instrument = position.Operation.Instrument
            }
          };

          await adapter.SendOrder(order);
        }
      }
    }
  }
}
