using Canvas.Core.ModelSpace;
using Canvas.Core.ShapeSpace;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using SkiaSharp;
using System;
using System.Collections.Generic;
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
  public partial class IndexPage
  {
    [Inject] IConfiguration Configuration { get; set; }

    /// <summary>
    /// Strategy
    /// </summary>
    string _buy = null;
    string _sell = null;
    const string _assetX = "GOOGL";
    const string _assetY = "GOOG";
    const string _account = "Simulation";

    protected virtual PageComponent View { get; set; }
    protected virtual PerformanceIndicator Performance { get; set; }

    protected override async Task OnAfterRenderAsync(bool setup)
    {
      if (setup)
      {
        var componentX = new ComponentModel { Color = SKColors.OrangeRed };
        var componentY = new ComponentModel { Color = SKColors.DeepSkyBlue };

        await View.ChartsView.Create(new GroupShape
        {
          Groups = new Dictionary<string, IGroupShape>
          {
            ["Prices"] = new GroupShape
            {
              Groups = new Dictionary<string, IGroupShape>
              {
                [nameof(OrderSideEnum.Buy)] = new ArrowShape(),
                [nameof(OrderSideEnum.Sell)] = new ArrowShape(),
                [$"{_assetX}:{nameof(PointModel.Ask)}"] = new LineShape { Component = componentX },
                [$"{_assetX}:{nameof(PointModel.Bid)}"] = new LineShape { Component = componentX },
                [$"{_assetY}:{nameof(PointModel.Ask)}"] = new LineShape { Component = componentY },
                [$"{_assetY}:{nameof(PointModel.Bid)}"] = new LineShape { Component = componentY }
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

      var performance = Performance.Calculate(new[] { account });
      var xPoint = seriesX.Last();
      var yPoint = seriesY.Last();
      var xAsk = xPoint.Ask;
      var xBid = xPoint.Bid;
      var yAsk = yPoint.Ask;
      var yBid = yPoint.Bid;
      var xSpread = xAsk - xBid;
      var ySpread = yAsk - yBid;

      if (account.ActivePositions.Count == 2)
      {
        var buy = account.ActivePositions[_buy];
        var sell = account.ActivePositions[_sell];
        var buyPoint = buy.Instrument.Points.Last();
        var sellPoint = sell.Instrument.Points.Last();
        var buyAsk = buyPoint.Ask;
        var buyBid = buyPoint.Bid;
        var sellAsk = sellPoint.Ask;
        var sellBid = sellPoint.Bid;
        var buySpread = buyAsk - buyBid;
        var sellSpread = sellAsk - sellBid;

        if ((buy.GainLossPointsEstimate + sell.GainLossPointsEstimate) > (buySpread + sellSpread))
        {
          _buy = null;
          _sell = null;
          ClosePositions();
        }
      }

      if (account.ActiveOrders.Any() is false && account.ActivePositions.Any() is false)
      {
        switch (true)
        {
          case true when (xBid - yAsk) >= (xSpread + ySpread): (_sell, _buy) = OpenPositions(instrumentY, instrumentX); break;
          case true when (yBid - xAsk) >= (xSpread + ySpread): (_sell, _buy) = OpenPositions(instrumentX, instrumentY); break;
        }
      }

      var chartPoints = new[]
      {
        new PointModel { Time = point.Time, Name = $"{_assetX}:{nameof(PointModel.Ask)}", Last = xAsk },
        new PointModel { Time = point.Time, Name = $"{_assetX}:{nameof(PointModel.Bid)}", Last = xBid },
        new PointModel { Time = point.Time, Name = $"{_assetY}:{nameof(PointModel.Ask)}", Last = yAsk },
        new PointModel { Time = point.Time, Name = $"{_assetY}:{nameof(PointModel.Bid)}", Last = yBid }
      };

      var reportPoints = new[]
      {
        new PointModel { Time = point.Time, Name = "Balance", Last = account.Balance },
        new PointModel { Time = point.Time, Name = "PnL", Last = performance.Last }
      };

      View.ChartsView.UpdateItems(chartPoints, 100);
      View.ReportsView.UpdateItems(reportPoints);
      View.DealsView.UpdateItems(account.Positions);
      View.OrdersView.UpdateItems(account.ActiveOrders);
      View.PositionsView.UpdateItems(account.ActivePositions);
    }

    private (string, string) OpenPositions(IInstrumentModel assetBuy, IInstrumentModel assetSell)
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

      return (messageSell.Next.Id, messageBuy.Next.Id);
    }

    private void ClosePositions()
    {
      foreach (var position in View.Adapter.Account.ActivePositions.Values)
      {
        View.Adapter.OrderStream.OnNext(new TransactionMessage<ITransactionOrderModel>
        {
          Action = ActionEnum.Create,
          Next = new TransactionOrderModel
          {
            Volume = 1,
            Side = Equals(position.Side, OrderSideEnum.Buy) ? OrderSideEnum.Sell : OrderSideEnum.Buy,
            Type = OrderTypeEnum.Market,
            Instrument = position.Instrument
          }
        });
      }
    }
  }
}
