using Board.Components;
using Canvas.Core.Models;
using Canvas.Core.Shapes;
using Core.Enums;
using Core.Indicators;
using Core.Models;
using Core.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Orleans;
using SkiaSharp;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Board.Pages.Shares
{
  public partial class Pairs
  {
    [Inject] IClusterClient Connector { get; set; }
    [Inject] IConfiguration Configuration { get; set; }
    [Inject] StreamService Streamer { get; set; }
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

        Observer.OnState += state =>
        {
          switch (true)
          {
            case true when state.Previous is SubscriptionEnum.None && state.Next is SubscriptionEnum.Progress: CreateAccounts(); break;
            case true when state.Previous is SubscriptionEnum.Progress && state.Next is SubscriptionEnum.Stream:

              TransactionsView.UpdateItems([.. View.Adapters.Values]);
              OrdersView.UpdateItems([.. View.Adapters.Values]);
              PositionsView.UpdateItems([.. View.Adapters.Values]);

              break;
          }
        };
      }

      await base.OnAfterRenderAsync(setup);
    }

    /// <summary>
    /// Accounts
    /// </summary>
    protected virtual void CreateAccounts()
    {
      Performance = new PerformanceIndicator { Name = "Balance" };

      var adapter = View.Adapters["Prime"] = new Simulation.Gateway
      {
        Speed = 1,
        Streamer = Streamer,
        Connector = Connector,
        Source = Configuration["Documents:Resources"],
        Account = new AccountModel
        {
          Name = "Demo",
          Balance = 25000,
          Instruments = new()
          {
            [assetX] = new InstrumentModel { Name = assetX },
            [assetY] = new InstrumentModel { Name = assetY }
          }
        }
      };

      adapter.OnData += OnPrice;
    }

    /// <summary>
    /// Stream
    /// </summary>
    /// <param name="price"></param>
    public virtual async Task OnPrice(PriceModel price)
    {
      var adapter = View.Adapters["Prime"];
      var account = adapter.Account;
      var instrumentX = account.Instruments[assetX];
      var instrumentY = account.Instruments[assetY];
      var seriesX = await adapter.GetTicks(new MetaModel { Count = 1, Instrument = instrumentX });
      var seriesY = await adapter.GetTicks(new MetaModel { Count = 1, Instrument = instrumentY });

      if (seriesX.Count is 0 || seriesY.Count is 0)
      {
        return;
      }

      var orders = await adapter.GetOrders(default);
      var positions = await adapter.GetPositions(default);
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

      TransactionsView.UpdateItems([.. View.Adapters.Values]);
      OrdersView.UpdateItems([.. View.Adapters.Values]);
      PositionsView.UpdateItems([.. View.Adapters.Values]);
      ChartsView.UpdateItems(price.Time.Value, "Prices", "Spread", new AreaShape { Y = range, Component = com });
      PerformanceView.UpdateItems(price.Time.Value, "Performance", "Balance", new AreaShape { Y = account.Balance + account.Performance });
      PerformanceView.UpdateItems(price.Time.Value, "Performance", "PnL", PerformanceView.GetShape<LineShape>(performance.Response, SKColors.OrangeRed));
    }

    /// <summary>
    /// Open positions
    /// </summary>
    /// <param name="assetBuy"></param>
    /// <param name="assetSell"></param>
    protected async Task OpenPositions(InstrumentModel assetBuy, InstrumentModel assetSell)
    {
      var adapter = View.Adapters["Prime"];
      var orderSell = new OrderModel
      {
        Amount = 1,
        Side = OrderSideEnum.Short,
        Type = OrderTypeEnum.Market,
        Operation = new() { Instrument = assetSell }
      };

      var orderBuy = new OrderModel
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
    public virtual async Task ClosePositions(Func<OrderModel, bool> condition = null)
    {
      var adapter = View.Adapters["Prime"];
      var positions = await adapter.GetPositions(default);
      var account = adapter.Account;

      foreach (var position in positions)
      {
        if (condition is null || condition(position))
        {
          var order = new OrderModel
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
