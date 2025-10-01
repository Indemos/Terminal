using Board.Components;
using Board.Services;
using Canvas.Core.Models;
using Canvas.Core.Shapes;
using Core.Enums;
using Core.Indicators;
using Core.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Orleans;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Board.Pages.Futures
{
  public partial class Leads
  {
    [Inject] IClusterClient Connector { get; set; }
    [Inject] IConfiguration Configuration { get; set; }
    [Inject] MessageService Messenger { get; set; }

    /// <summary>
    /// Strategy
    /// </summary>
    protected virtual ControlsComponent View { get; set; }
    protected virtual ChartsComponent LeaderView { get; set; }
    protected virtual ChartsComponent FollowerView { get; set; }
    protected virtual ChartsComponent IndicatorsView { get; set; }
    protected virtual ChartsComponent PerformanceView { get; set; }
    protected virtual TransactionsComponent DealsView { get; set; }
    protected virtual OrdersComponent OrdersView { get; set; }
    protected virtual PositionsComponent PositionsView { get; set; }
    protected virtual StatementsComponent StatementsView { get; set; }
    protected virtual PerformanceIndicator Performance { get; set; }
    protected virtual IDictionary<string, ScaleIndicator> Scales { get; set; }
    protected virtual PriceModel PreviousLeader { get; set; }
    protected virtual PriceModel PreviousFollower { get; set; }

    protected override async Task OnAfterRenderAsync(bool setup)
    {
      if (setup)
      {
        await LeaderView.Create("Prices");
        await FollowerView.Create("Prices");
        await IndicatorsView.Create("Indicators");
        await PerformanceView.Create("Performance");

        LeaderView.Composers.ForEach(o => o.ShowIndex = i => GetDateByIndex(o.Items, (int)i));
        FollowerView.Composers.ForEach(o => o.ShowIndex = i => GetDateByIndex(o.Items, (int)i));
        IndicatorsView.Composers.ForEach(o => o.ShowIndex = i => GetDateByIndex(o.Items, (int)i));
        PerformanceView.Composers.ForEach(o => o.ShowIndex = i => GetDateByIndex(o.Items, (int)i));

        Messenger.OnMessage += state =>
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

    protected virtual void CreateAccounts()
    {
      var adapter = View.Adapters["Prime"] = new Simulation.Gateway
      {
        Speed = 1,
        Connector = Connector,
        Source = "D:/Code/NET/Terminal/Data/FUTS/2025-06-17", // Configuration["Simulation:Source"]
        Account = new AccountModel
        {
          Name = "Demo",
          Balance = 25000,
          Instruments = new()
          {
            ["ESU25"] = new() { Name = "ESU25", StepValue = 12.50, StepSize = 0.25, Leverage = 50, Commission = 3.65 },
            ["NQU25"] = new() { Name = "NQU25", StepValue = 5, StepSize = 0.25, Leverage = 20 },
          }
        }
      };

      Performance = new PerformanceIndicator { Name = "Balance" };
      Scales = adapter.Account.Instruments.Keys.ToDictionary(o => o, o => new ScaleIndicator { Name = o, Min = -1, Max = 1 });

      //adapter.Stream += o => OnPrice(o);
    }

    public virtual async void OnPrice(PriceModel price)
    {
      var adapter = View.Adapters["Prime"];
      var account = adapter.Account;
      var assetX = account.Instruments["NQU25"];
      var assetY = account.Instruments["ESU25"];
      var seriesX = await adapter.Ticks(new MetaModel { Count = 1, Instrument = assetX });
      var seriesY = await adapter.Ticks(new MetaModel { Count = 1, Instrument = assetY });

      if (seriesX.Count is 0 || seriesY.Count is 0)
      {
        return;
      }

      var orders = await adapter.Orders(default);
      var positions = await adapter.Positions(default);
      var performance = await Performance.Update([adapter]);
      var scaleX = await Scales["NQU25"].Update(seriesX);
      var scaleY = await Scales["ESU25"].Update(seriesY);
      var priceX = seriesX.Last();
      var priceY = seriesY.Last();
      var spread = Math.Abs((scaleX.Response.Last - scaleY.Response.Last).Value);

      if (orders.Count is 0 && positions.Count is 0 && spread > 0.1 && spread < 1)
      {
        var isLong = scaleX.Response.Last > PreviousLeader.Last && scaleX.Response.Last > scaleY.Response.Last;
        var isShort = scaleX.Response.Last < PreviousLeader.Last && scaleX.Response.Last < scaleY.Response.Last;

        switch (true)
        {
          case true when isLong: await OpenPositions(assetY, OrderSideEnum.Long); break;
          case true when isShort: await OpenPositions(assetY, OrderSideEnum.Short); break;
        }
      }

      if (positions.Count is not 0)
      {
        var pos = positions.First();
        var closeLong = pos.Side is OrderSideEnum.Long && scaleX.Response.Last < scaleY.Response.Last;
        var closeShort = pos.Side is OrderSideEnum.Short && scaleX.Response.Last > scaleY.Response.Last;

        if (closeLong || closeShort)
        {
          await ClosePositions();
        }
      }

      PreviousLeader = scaleX.Response with { };
      PreviousFollower = scaleY.Response with { };

      var com = new ComponentModel { Color = SKColors.LimeGreen };
      var comUp = new ComponentModel { Color = SKColors.DeepSkyBlue };
      var comDown = new ComponentModel { Color = SKColors.OrangeRed };

      DealsView.UpdateItems([.. View.Adapters.Values]);
      OrdersView.UpdateItems([.. View.Adapters.Values]);
      PositionsView.UpdateItems([.. View.Adapters.Values]);
      LeaderView.UpdateItems(price.Time.Value, "Prices", "Leader", LeaderView.GetShape<CandleShape>(priceX));
      FollowerView.UpdateItems(price.Time.Value, "Prices", "Spread", new AreaShape { Y = spread, Component = com });
      IndicatorsView.UpdateItems(price.Time.Value, "Indicators", "X", new LineShape { Y = scaleX.Response.Last, Component = comUp });
      IndicatorsView.UpdateItems(price.Time.Value, "Indicators", "Y", new LineShape { Y = scaleY.Response.Last, Component = comDown });
      PerformanceView.UpdateItems(price.Time.Value, "Performance", "Balance", new AreaShape { Y = account.Balance + account.Performance });
      PerformanceView.UpdateItems(price.Time.Value, "Performance", "PnL", PerformanceView.GetShape<LineShape>(performance.Response, SKColors.OrangeRed));
    }

    /// <summary>
    /// Open positions
    /// </summary>
    /// <param name="assetBuy"></param>
    /// <param name="assetSell"></param>
    protected async Task OpenPositions(InstrumentModel asset, OrderSideEnum side)
    {
      var adapter = View.Adapters["Prime"];
      var order = new OrderModel
      {
        Amount = 1,
        Side = side,
        Type = OrderTypeEnum.Market,
        Operation = new() { Instrument = asset }
      };

      await adapter.SendOrder(order);
    }

    /// <summary>
    /// Close positions
    /// </summary>
    /// <param name="condition"></param>
    public virtual async Task ClosePositions(Func<OrderModel, bool> condition = null)
    {
      var adapter = View.Adapters["Prime"];
      var positions = await adapter.Positions(default);
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
