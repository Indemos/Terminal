using Alpaca;
using Canvas.Core.Models;
using Canvas.Core.Shapes;
using Client.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Extensions;
using Terminal.Core.Indicators;
using Terminal.Core.Models;

namespace Client.Pages
{
  public partial class Orders
  {
    [Inject] IConfiguration Configuration { get; set; }

    /// <summary>
    /// Strategy
    /// </summary>
    const string _asset = "FAKEPACA";

    protected virtual PageComponent View { get; set; }
    protected virtual PerformanceIndicator Performance { get; set; }
    protected virtual IGateway Adapter
    {
      get => View.Adapters.Get("Demo");
      set => View.Adapters["Demo"] = value;
    }

    protected override async Task OnAfterRenderAsync(bool setup)
    {
      if (setup)
      {
        await CreateViews();

        View.OnPreConnect = CreateAccounts;
        View.OnPostConnect = () =>
        {
          var account = Adapter.Account;

          View.DealsView.UpdateItems(account.Positions);
          View.OrdersView.UpdateItems(account.ActiveOrders);
          View.PositionsView.UpdateItems(account.ActivePositions);
        };
      }

      await base.OnAfterRenderAsync(setup);
    }

    protected virtual async Task CreateViews()
    {
      var indUp = new ComponentModel { Color = SKColors.DeepSkyBlue };
      var indDown = new ComponentModel { Color = SKColors.OrangeRed };
      var indAreas = new Shape();
      var indCharts = new Shape();

      indCharts.Groups["Ups"] = new BarShape { Component = indUp };
      indCharts.Groups["Downs"] = new BarShape { Component = indDown };
      indAreas.Groups["Prices"] = indCharts;

      await View.ChartsView.Create(indAreas);

      var pnlGain = new ComponentModel { Color = SKColors.OrangeRed, Size = 5 };
      var pnlBalance = new ComponentModel { Color = SKColors.Black };
      var pnlAreas = new Shape();
      var pnlCharts = new Shape();

      pnlCharts.Groups["PnL"] = new LineShape { Component = pnlGain };
      pnlCharts.Groups["Balance"] = new AreaShape { Component = pnlBalance };
      pnlAreas.Groups["Performance"] = pnlCharts;

      await View.ReportsView.Create(pnlAreas);
    }

    protected virtual void CreateAccounts()
    {
      var account = new Account
      {
        Descriptor = "Demo",
        Instruments = new Dictionary<string, InstrumentModel>
        {
          [_asset] = new InstrumentModel { Name = _asset }
        }
      };

      Adapter = new Adapter
      {
        Account = account,
        ConsumerKey = Configuration["Alpaca:Token"],
        ConsumerSecret = Configuration["Alpaca:Secret"],
        StreamUri = "wss://stream.data.alpaca.markets/v2/test"
      };

      Performance = new PerformanceIndicator { Name = "Balance" };

      account
        .Instruments
        .Values
        .ForEach(o => o.Points.CollectionChanged += (_, e) => e
          .NewItems
          .OfType<PointModel>()
          .ForEach(async o => await OnData(o)));
    }

    private async Task OnData(PointModel point)
    {
      var account = Adapter.Account;
      var instrument = account.Instruments[_asset];
      var series = instrument.Points;

      if (series.Count > 1)
      {
        return;
      }

      var chartPoints = new List<KeyValuePair<string, PointModel>>();
      var reportPoints = new List<KeyValuePair<string, PointModel>>();
      var performance = Performance.Calculate([account]);

      chartPoints.Add(KeyValuePair.Create("Ups", new PointModel { Time = point.Time, Last = Math.Max(0, point.Last.Value) }));
      reportPoints.Add(KeyValuePair.Create("Balance", new PointModel { Time = point.Time, Last = account.Balance }));
      reportPoints.Add(KeyValuePair.Create("PnL", new PointModel { Time = point.Time, Last = performance.Point.Last }));

      await View.ChartsView.UpdateItems(chartPoints, 100);
      await View.ReportsView.UpdateItems(reportPoints);
      await View.DealsView.UpdateItems(account.Positions);
      await View.OrdersView.UpdateItems(account.ActiveOrders);
      await View.PositionsView.UpdateItems(account.ActivePositions);
    }

    private void OpenPositions(InstrumentModel assetBuy, InstrumentModel assetSell)
    {
      var orderSell = new OrderModel
      {
        Side = OrderSideEnum.Sell,
        Type = OrderTypeEnum.Market,
        Transaction = new()
        {
          Volume = 1,
          Instrument = assetSell
        }
      };

      var orderBuy = new OrderModel
      {
        Side = OrderSideEnum.Buy,
        Type = OrderTypeEnum.Market,
        Transaction = new()
        {
          Volume = 1,
          Instrument = assetBuy
        }
      };

      Adapter.CreateOrders(orderBuy);
      Adapter.CreateOrders(orderSell);

      var account = Adapter.Account;
      var buy = account.ActivePositions.Values.First(o => o.Order.Side == OrderSideEnum.Buy);
      var sell = account.ActivePositions.Values.First(o => o.Order.Side == OrderSideEnum.Sell);

      //points.Add(new PointModel { Time = buy.Time, Name = nameof(OrderSideEnum.Buy), Last = buy.OpenPrices.Last().Price });
      //points.Add(new PointModel { Time = sell.Time, Name = nameof(OrderSideEnum.Sell), Last = sell.OpenPrices.Last().Price });
    }

    private void ClosePositions()
    {
      foreach (var position in Adapter.Account.ActivePositions.Values)
      {
        var side = OrderSideEnum.Buy;

        if (Equals(position.Order.Side, OrderSideEnum.Buy))
        {
          side = OrderSideEnum.Sell;
        }

        var order = new OrderModel
        {
          Side = side,
          Type = OrderTypeEnum.Market,
          Transaction = new()
          {
            Volume = position.Order.Transaction.Volume,
            Instrument = position.Order.Transaction.Instrument
          }
        };

        Adapter.CreateOrders(order);

        //points.Add(new PointModel { Time = order.Time, Name = nameof(OrderSideEnum.Buy), Last = price });
      }
    }
  }
}
