using Canvas.Core.Models;
using Canvas.Core.Shapes;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Schedule.Runners;
using SkiaSharp;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Client.Components;
using Terminal.Connector.Simulation;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Indicators;
using Terminal.Core.Models;
using Terminal.Core.Services;
using System;

namespace Client.Pages
{
  public partial class Pairs
  {
    [Inject] IConfiguration Configuration { get; set; }

    /// <summary>
    /// Strategy
    /// </summary>
    const string _assetX = "GOOGL";
    const string _assetY = "GOOG";
    const string _account = "Simulation";

    protected virtual IAccount Account { get; set; }
    protected virtual PageComponent View { get; set; }
    protected virtual PerformanceIndicator Performance { get; set; }

    protected override async Task OnAfterRenderAsync(bool setup)
    {
      if (setup)
      {
        var comUp = new ComponentModel { Color = SKColors.DeepSkyBlue };
        var comDown = new ComponentModel { Color = SKColors.OrangeRed };

        await View.ChartsView.Create(new GroupShape
        {
          Groups = new Dictionary<string, IGroupShape>
          {
            ["Prices"] = new GroupShape
            {
              Groups = new Dictionary<string, IGroupShape>
              {
                [nameof(OrderSideEnum.Buy)] = new ArrowShape { Component = comUp },
                [nameof(OrderSideEnum.Sell)] = new ArrowShape { Component = comDown },
                ["Range"] = new AreaShape { Component = comUp }
              }
            }
          }
        });

        var componentGain = new ComponentModel { Color = SKColors.OrangeRed, Size = 5 };
        var componentBalance = new ComponentModel { Color = SKColors.Black };

        await View.ReportsView.Create(new GroupShape
        {
          Groups = new Dictionary<string, IGroupShape>
          {
            ["Performance"] = new GroupShape
            {
              Groups = new Dictionary<string, IGroupShape>
              {
                ["PnL"] = new LineShape { Component = componentGain },
                ["Balance"] = new AreaShape { Component = componentBalance }
              }
            }
          }
        });

        View.Setup = () =>
        {
          Account = new Account
          {
            Balance = 25000,
            Name = _account,
            Instruments = new Dictionary<string, Instrument>
            {
              [_assetX] = new Instrument { Name = _assetX },
              [_assetY] = new Instrument { Name = _assetY }
            }
          };

          View.Adapter = new Adapter
          {
            Speed = 1,
            Account = Account,
            Source = Configuration["Simulation:Source"]
          };

          Performance = new PerformanceIndicator { Name = "Balance" };

          Account
            .Instruments
            .Values
            .ForEach(o => o.Points.CollectionChanged += (o, e) =>
            {
              foreach (PointModel item in e.NewItems)
              {
                InstanceService<BackgroundRunner>.Instance.Send(OnData(item));
              }
            });
        };
      }

      await base.OnAfterRenderAsync(setup);
    }

    private async Task OnData(PointModel point)
    {
      var instrumentX = Account.Instruments[_assetX];
      var instrumentY = Account.Instruments[_assetY];
      var seriesX = instrumentX.Points;
      var seriesY = instrumentY.Points;

      if (seriesX.Any() is false || seriesY.Any() is false)
      {
        return;
      }

      var chartPoints = new List<KeyValuePair<string, PointModel>>();
      var reportPoints = new List<KeyValuePair<string, PointModel>>();
      var performance = Performance.Calculate(new[] { Account });
      var xPoint = seriesX.Last();
      var yPoint = seriesY.Last();
      var xAsk = xPoint.Ask;
      var xBid = xPoint.Bid;
      var yAsk = yPoint.Ask;
      var yBid = yPoint.Bid;
      var spread = (xAsk - xBid) + (yAsk - yBid);
      var expenses = spread * 2;

      if (Account.ActivePositions.Count == 2)
      {
        var buy = Account.ActivePositions.Values.First(o => o.Order.Side == OrderSideEnum.Buy);
        var sell = Account.ActivePositions.Values.First(o => o.Order.Side == OrderSideEnum.Sell);

        switch (true)
        {
          case true when (buy.GainLossPointsEstimate + sell.GainLossPointsEstimate) > expenses: ClosePositions(); break;
          case true when (buy.GainLossPointsEstimate + sell.GainLossPointsEstimate) < -expenses: OpenPositions(buy.Order.Transaction.Instrument, sell.Order.Transaction.Instrument); break;
        }
      }

      if (Account.ActiveOrders.Any() is false && Account.ActivePositions.Any() is false)
      {
        switch (true)
        {
          case true when (xBid - yAsk) > expenses: OpenPositions(instrumentY, instrumentX); break;
          case true when (yBid - xAsk) > expenses: OpenPositions(instrumentX, instrumentY); break;
        }
      }

      var range = Math.Max(
        ((xBid - yAsk) - expenses).Value,
        ((yBid - xAsk) - expenses).Value);

      chartPoints.Add(KeyValuePair.Create("Range", new PointModel { Time = point.Time, Last = Math.Max(0, range) }));
      reportPoints.Add(KeyValuePair.Create("Balance", new PointModel { Time = point.Time, Last = Account.Balance }));
      reportPoints.Add(KeyValuePair.Create("PnL", new PointModel { Time = point.Time, Last = performance.Point.Last }));

      await View.ChartsView.UpdateItems(chartPoints, 100);
      await View.ReportsView.UpdateItems(reportPoints);
      await View.DealsView.UpdateItems(Account.Positions);
      await View.OrdersView.UpdateItems(Account.ActiveOrders);
      await View.PositionsView.UpdateItems(Account.ActivePositions);
    }

    private (string, string) OpenPositions(IInstrument assetBuy, IInstrument assetSell)
    {
      var messageSell = new StateModel<OrderModel>
      {
        Action = ActionEnum.Create,
        Next = new OrderModel
        {
          Side = OrderSideEnum.Sell,
          Type = OrderTypeEnum.Market,
          Transaction = new()
          {
            Volume = 1,
            Instrument = assetSell
          }
        }
      };

      var messageBuy = new StateModel<OrderModel>
      {
        Action = ActionEnum.Create,
        Next = new OrderModel
        {
          Side = OrderSideEnum.Buy,
          Type = OrderTypeEnum.Market,
          Transaction = new()
          {
            Volume = 1,
            Instrument = assetBuy
          }
        }
      };

      View.Adapter.OrderStream(messageSell);
      View.Adapter.OrderStream(messageBuy);

      var account = View.Adapter.Account;
      var buy = account.ActivePositions.Values.First(o => o.Order.Side == OrderSideEnum.Buy);
      var sell = account.ActivePositions.Values.First(o => o.Order.Side == OrderSideEnum.Sell);

      //points.Add(new PointModel { Time = buy.Time, Name = nameof(OrderSideEnum.Buy), Last = buy.OpenPrices.Last().Price });
      //points.Add(new PointModel { Time = sell.Time, Name = nameof(OrderSideEnum.Sell), Last = sell.OpenPrices.Last().Price });

      return (messageSell.Next.Transaction.Id, messageBuy.Next.Transaction.Id);
    }

    private void ClosePositions()
    {
      foreach (var position in View.Adapter.Account.ActivePositions.Values)
      {
        var side = OrderSideEnum.Buy;
        var point = position.Order.Transaction.Instrument.Points.Last();
        var price = point.Ask;

        if (Equals(position.Order.Side, OrderSideEnum.Buy))
        {
          price = point.Bid;
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

        View.Adapter.OrderStream(new StateModel<OrderModel>
        {
          Action = ActionEnum.Create,
          Next = order
        });

        //points.Add(new PointModel { Time = order.Time, Name = nameof(OrderSideEnum.Buy), Last = price });
      }
    }
  }
}
