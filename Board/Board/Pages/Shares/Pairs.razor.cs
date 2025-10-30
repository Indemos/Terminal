using Board.Components;
using Canvas.Core.Extensions;
using Canvas.Core.Models;
using Canvas.Core.Shapes;
using Core.Conventions;
using Core.Enums;
using Core.Indicators;
using Core.Models;
using Core.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Orleans;
using Simulation;
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
    [Inject] MessageService Messenger { get; set; }
    [Inject] StateService State { get; set; }

    ControlsComponent View { get; set; }
    ChartsComponent DataView { get; set; }
    ChartsComponent PerformanceView { get; set; }
    OrdersComponent OrdersView { get; set; }
    PositionsComponent PositionsView { get; set; }
    StatementsComponent StatementsView { get; set; }
    TransactionsComponent TransactionsView { get; set; }
    PerformanceIndicator Performance { get; set; }

    const string assetX = "GOOG";
    const string assetY = "GOOGL";

    IGateway Adapter
    {
      get => View.Adapters.Get(string.Empty) as IGateway;
      set => View.Adapters[string.Empty] = value;
    }

    protected override async Task OnAfterRenderAsync(bool setup)
    {
      if (setup)
      {
        await DataView.Create("Prices");
        await PerformanceView.Create("Performance");

        Messenger.Subscribe<PriceModel>(Update);
        State.Subscribe(state =>
        {
          if (state.Previous is SubscriptionEnum.None && state.Next is SubscriptionEnum.Progress)
          {
            Performance = new PerformanceIndicator();
            Adapter = new SimGateway
            {
              Messenger = Messenger,
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
          }

          return Task.CompletedTask;
        });
      }
    }

    /// <summary>
    /// Stream
    /// </summary>
    /// <param name="price"></param>
    async Task Update(PriceModel price)
    {
      var account = Adapter.Account;
      var instrumentX = account.Instruments[assetX];
      var instrumentY = account.Instruments[assetY];
      var seriesX = await Adapter.GetTicks(new MetaModel { Count = 1, Instrument = instrumentX });
      var seriesY = await Adapter.GetTicks(new MetaModel { Count = 1, Instrument = instrumentY });

      if (seriesX.Count is 0 || seriesY.Count is 0)
      {
        return;
      }

      var orders = await Adapter.GetOrders(default);
      var positions = await Adapter.GetPositions(default);
      var performance = await Performance.Update(View.Adapters.Values);
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

      TransactionsView.Update(View.Adapters.Values);
      OrdersView.Update(View.Adapters.Values);
      PositionsView.Update(View.Adapters.Values);
      DataView.Update(price.Time.Value, "Prices", "Spread", new AreaShape { Y = range, Component = com });
      PerformanceView.Update(price.Time.Value, "Performance", "Balance", new AreaShape { Y = account.Balance + account.Performance });
      PerformanceView.Update(price.Time.Value, "Performance", "PnL", PerformanceView.GetShape<LineShape>(performance.Response, SKColors.OrangeRed));
    }

    /// <summary>
    /// Open positions
    /// </summary>
    /// <param name="assetBuy"></param>
    /// <param name="assetSell"></param>
    async Task OpenPositions(InstrumentModel assetBuy, InstrumentModel assetSell)
    {
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

      await Adapter.SendOrder(orderBuy);
      await Adapter.SendOrder(orderSell);
    }

    /// <summary>
    /// Close positions
    /// </summary>
    /// <param name="condition"></param>
    async Task ClosePositions(Func<OrderModel, bool> condition = null)
    {
      var account = Adapter.Account;
      var positions = await Adapter.GetPositions(default);

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

          await Adapter.SendOrder(order);
        }
      }
    }
  }
}
