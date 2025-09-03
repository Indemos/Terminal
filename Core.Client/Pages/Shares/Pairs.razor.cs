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
using System.Collections.Generic;
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
            case true when state.Previous is SubscriptionEnum.None && state.Next is SubscriptionEnum.Progress: CreateAccounts(); break;
            case true when state.Previous is SubscriptionEnum.Progress && state.Next is SubscriptionEnum.Stream:

              var account = View.Adapters["Prime"].Account;

              await TransactionsView.UpdateItems([.. View.Adapters.Values]);
              await OrdersView.UpdateItems([.. View.Adapters.Values]);
              await PositionsView.UpdateItems([.. View.Adapters.Values]);

              break;
          }
        };
      }

      await base.OnAfterRenderAsync(setup);
    }

    protected virtual void CreateAccounts()
    {
      var account = new AccountState
      {
        Balance = 25000,
        Descriptor = "Demo",
        Instruments = new Dictionary<string, InstrumentState>
        {
          [assetX] = new InstrumentState { Name = assetX },
          [assetY] = new InstrumentState { Name = assetY }
        },
      };

      View.Adapters["Prime"] = new Adapter
      {
        Speed = 1,
        Account = account,
        Connector = Connector,
        Source = Configuration["Simulation:Source"]
      };

      Performance = new PerformanceIndicator { Name = "Balance" };

      View
        .Adapters
        .Values
        .ForEach(adapter => adapter.Stream.SubscribeAsync(OnData));
    }

    protected async Task OnData(PriceState point, StreamSequenceToken token)
    {
      var adapter = View.Adapters["Prime"];
      var account = adapter.Account;
      var instrumentX = account.Instruments[assetX];
      var instrumentY = account.Instruments[assetY];
      var seriesX = (await adapter.GetTicks(new ConditionState { Count = 1, Instrument = instrumentX })).Data;
      var seriesY = (await adapter.GetTicks(new ConditionState { Count = 1, Instrument = instrumentY })).Data;

      if (seriesX.Count is 0 || seriesY.Count is 0)
      {
        return;
      }

      var orders = (await adapter.GetOrders()).Data;
      var positions = (await adapter.GetPositions()).Data;
      var performance = await Performance.Update([adapter]);
      var xPoint = seriesX.Last();
      var yPoint = seriesY.Last();
      var spread = (xPoint.Ask - xPoint.Bid) + (yPoint.Ask - yPoint.Bid);
      var expenses = spread;
      var posAmount = positions.Sum(o => o.Operation.Amount);

      if (orders.Count is 0)
      {
        if (positions.Count == 2)
        {
          var buy = positions.First(o => o.Side is OrderSideEnum.Long);
          var sell = positions.First(o => o.Side is OrderSideEnum.Short);
          var gain = buy.Gain + sell.Gain;

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
      await ChartsView.UpdateItems(point.Time.Value.Ticks, "Prices", "Spread", new AreaShape { Y = range, Component = com });
      await PerformanceView.UpdateItems(point.Time.Value.Ticks, "Performance", "Balance", new AreaShape { Y = account.Balance + account.Performance });
      await PerformanceView.UpdateItems(point.Time.Value.Ticks, "Performance", "PnL", PerformanceView.GetShape<LineShape>(performance.Response, SKColors.OrangeRed));
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
    /// <returns></returns>
    public virtual async Task ClosePositions(Func<OrderState, bool> condition = null)
    {
      var adapter = View.Adapters["Prime"];
      var positions = (await adapter.GetPositions()).Data;
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
