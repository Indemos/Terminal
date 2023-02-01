using Canvas.Core.ModelSpace;
using Canvas.Core.ShapeSpace;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Terminal.Client.Components;
using Terminal.Connector.Simulation;
using Terminal.Core.CollectionSpace;
using Terminal.Core.EnumSpace;
using Terminal.Core.IndicatorSpace;
using Terminal.Core.MessageSpace;
using Terminal.Core.ModelSpace;

namespace Terminal.Client.Pages
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
                [nameof(OrderSideEnum.Buy)] = new ArrowShape { Component = comUp},
                [nameof(OrderSideEnum.Sell)] = new ArrowShape { Component = comDown },
                [$"{_assetX}:{nameof(PointModel.Ask)}"] = new LineShape { Component = comUp },
                [$"{_assetX}:{nameof(PointModel.Bid)}"] = new LineShape { Component = comUp },
                [$"{_assetY}:{nameof(PointModel.Ask)}"] = new LineShape { Component = comDown },
                [$"{_assetY}:{nameof(PointModel.Bid)}"] = new LineShape { Component = comDown }
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
          var account = new AccountModel
          {
            Balance = 25000,
            Name = _account,
            Instruments = new NameCollection<string, IInstrumentModel>
            {
              [_assetX] = new InstrumentModel { Name = _assetX },
              [_assetY] = new InstrumentModel { Name = _assetY }
            }
          };

          View.Adapter = new Adapter
          {
            Speed = 1,
            Name = _account,
            Account = account,
            Source = Configuration["Simulation:Source"]
          };

          Performance = new PerformanceIndicator { Name = "Balance" };

          account
            .Instruments
            .Values
            .Select(o => o.Points.ItemStream)
            .Merge()
            .Subscribe(OnData);
        };
      }

      await base.OnAfterRenderAsync(setup);
    }

    private void OnData(ITransactionMessage<IPointModel> message)
    {
      var point = message.Next;
      var account = point.Account;
      var instrumentX = point.Account.Instruments[_assetX];
      var instrumentY = point.Account.Instruments[_assetY];
      var seriesX = instrumentX.Points;
      var seriesY = instrumentY.Points;

      if (seriesX.Any() is false || seriesY.Any() is false)
      {
        return;
      }

      var chartPoints = new List<IPointModel>();
      var reportPoints = new List<IPointModel>();
      var performance = Performance.Calculate(new[] { account });
      var xPoint = seriesX.Last();
      var yPoint = seriesY.Last();
      var xAsk = xPoint.Ask;
      var xBid = xPoint.Bid;
      var yAsk = yPoint.Ask;
      var yBid = yPoint.Bid;
      var expenses = (xAsk - xBid) + (yAsk - yBid);

      if (account.ActivePositions.Count == 2)
      {
        var buy = account.ActivePositions.Values.First(o => o.Side == OrderSideEnum.Buy);
        var sell = account.ActivePositions.Values.First(o => o.Side == OrderSideEnum.Sell);

        switch (true)
        {
          case true when (buy.GainLossPointsEstimate + sell.GainLossPointsEstimate) > expenses: ClosePositions(chartPoints); break;
          case true when (buy.GainLossPointsEstimate + sell.GainLossPointsEstimate) < -expenses: OpenPositions(buy.Instrument, sell.Instrument, chartPoints); break;
        }
      }

      if (account.ActiveOrders.Any() is false && account.ActivePositions.Any() is false)
      {
        switch (true)
        {
          case true when (xBid - yAsk) >= expenses: OpenPositions(instrumentY, instrumentX, chartPoints); break;
          case true when (yBid - xAsk) >= expenses: OpenPositions(instrumentX, instrumentY, chartPoints); break;
        }
      }

      chartPoints.Add(new PointModel { Time = point.Time, Name = $"{_assetX}:{nameof(PointModel.Ask)}", Last = xAsk });
      chartPoints.Add(new PointModel { Time = point.Time, Name = $"{_assetX}:{nameof(PointModel.Bid)}", Last = xBid });
      chartPoints.Add(new PointModel { Time = point.Time, Name = $"{_assetY}:{nameof(PointModel.Ask)}", Last = yAsk });
      chartPoints.Add(new PointModel { Time = point.Time, Name = $"{_assetY}:{nameof(PointModel.Bid)}", Last = yBid });

      reportPoints.Add(new PointModel { Time = point.Time, Name = "Balance", Last = account.Balance });
      reportPoints.Add(new PointModel { Time = point.Time, Name = "PnL", Last = performance.Last });

      View.ChartsView.UpdateItems(chartPoints, 100);
      View.ReportsView.UpdateItems(reportPoints);
      View.DealsView.UpdateItems(account.Positions);
      View.OrdersView.UpdateItems(account.ActiveOrders);
      View.PositionsView.UpdateItems(account.ActivePositions);
    }

    private (string, string) OpenPositions(IInstrumentModel assetBuy, IInstrumentModel assetSell, IList<IPointModel> points)
    {
      var messageSell = new TransactionMessage<ITransactionOrderModel>
      {
        Action = ActionEnum.Create,
        Next = new TransactionOrderModel
        {
          Volume = 1,
          Side = OrderSideEnum.Sell,
          Type = OrderTypeEnum.Market,
          Instrument = assetSell
        }
      };

      var messageBuy = new TransactionMessage<ITransactionOrderModel>
      {
        Action = ActionEnum.Create,
        Next = new TransactionOrderModel
        {
          Volume = 1,
          Side = OrderSideEnum.Buy,
          Type = OrderTypeEnum.Market,
          Instrument = assetBuy
        }
      };

      View.Adapter.OrderStream.OnNext(messageSell);
      View.Adapter.OrderStream.OnNext(messageBuy);

      var account = View.Adapter.Account;
      var buy = account.ActivePositions.Values.First(o => o.Side == OrderSideEnum.Buy);
      var sell = account.ActivePositions.Values.First(o => o.Side == OrderSideEnum.Sell);

      //points.Add(new PointModel { Time = buy.Time, Name = nameof(OrderSideEnum.Buy), Last = buy.OpenPrices.Last().Price });
      //points.Add(new PointModel { Time = sell.Time, Name = nameof(OrderSideEnum.Sell), Last = sell.OpenPrices.Last().Price });

      return (messageSell.Next.Id, messageBuy.Next.Id);
    }

    private void ClosePositions(IList<IPointModel> points)
    {
      foreach (var position in View.Adapter.Account.ActivePositions.Values)
      {
        var side = OrderSideEnum.Buy;
        var point = position.Instrument.Points.Last();
        var price = point.Ask;

        if (Equals(position.Side, OrderSideEnum.Buy))
        {
          price = point.Bid;
          side = OrderSideEnum.Sell;
        }

        var order = new TransactionOrderModel
        {
          Side = side,
          Volume = position.Volume,
          Type = OrderTypeEnum.Market,
          Instrument = position.Instrument
        };

        View.Adapter.OrderStream.OnNext(new TransactionMessage<ITransactionOrderModel>
        {
          Action = ActionEnum.Create,
          Next = order
        });

        //points.Add(new PointModel { Time = order.Time, Name = nameof(OrderSideEnum.Buy), Last = price });
      }
    }
  }
}
