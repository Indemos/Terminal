using Core.CollectionSpace;
using Core.EnumSpace;
using Core.IndicatorSpace;
using Core.MessageSpace;
using Core.ModelSpace;
using Gateway.Simulation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Presentation.StrategySpace
{
  public class ComboStrategy : BaseStrategy
  {
    const string _assetX = "GOOG";
    const string _assetY = "GOOGL";
    const string _account = "Simulation";

    ScaleIndicator _scaleIndicatorX = null;
    ScaleIndicator _scaleIndicatorY = null;
    PerformanceIndicator _performanceIndicator = null;

    public override Task OnLoad()
    {
      var span = TimeSpan.FromMinutes(1);
      var instrumentX = new InstrumentModel { Name = _assetX, TimeFrame = span };
      var instrumentY = new InstrumentModel { Name = _assetY, TimeFrame = span };

      var account = new AccountModel
      {
        Balance = 50000,
        Name = _account,
        Instruments = new NameCollection<string, IInstrumentModel>
        {
          [_assetX] = instrumentX,
          [_assetY] = instrumentY
        }
      };

      var gateway = new GatewayClient
      {
        Name = _account,
        Account = account,
        Evaluate = Parse,
        Source = "D:/Code/Net/Terminal/Data/Quotes"
      };

      _performanceIndicator = new PerformanceIndicator { Name = "Balance" };
      _scaleIndicatorX = new ScaleIndicator { Max = 1, Min = -1, Interval = 1, Name = "Indicators : " + _assetX };
      _scaleIndicatorY = new ScaleIndicator { Max = 1, Min = -1, Interval = 1, Name = "Indicators : " + _assetY };

      gateway
        .Account
        .Instruments
        .Values
        .Select(o => o.PointGroups.ItemStream)
        .Merge()
        .TakeUntil(_subscriptions)
        .Subscribe(OnData);

      CreateCharts(instrumentX, instrumentY);
      CreateGateways(gateway);

      return Task.FromResult(0);
    }

    protected void OnData(ITransactionMessage<IPointModel> message)
    {
      var point = message.Next;
      var account = point.Account;
      var gateway = Gateways.First();
      var instrumentX = point.Account.Instruments[_assetX];
      var instrumentY = point.Account.Instruments[_assetY];
      var seriesX = instrumentX.PointGroups;
      var seriesY = instrumentY.PointGroups;
      var indicatorX = _scaleIndicatorX.Calculate(seriesX).Bar.Close;
      var indicatorY = _scaleIndicatorY.Calculate(seriesY).Bar.Close;
      //var balanceIndicator = _balanceIndicator.Calculate(Gateways.Select(o => o.Account), point).Close;

      if (seriesX.Any() && seriesY.Any())
      {
        if (account.ActiveOrders.Any() == false &&
            account.ActivePositions.Any() == false && 
            Math.Abs(indicatorX.Value - indicatorY.Value) >= 0.5)
        {
          if (indicatorX > indicatorY)
          {
            gateway.OrderSenderStream.OnNext(new TransactionMessage<ITransactionOrderModel>
            {
              Action = ActionEnum.Create,
              Next = new TransactionOrderModel
              {
                Size = 1,
                Type = TransactionTypeEnum.Sell,
                Instrument = instrumentX
              }
            });

            gateway.OrderSenderStream.OnNext(new TransactionMessage<ITransactionOrderModel>
            {
              Action = ActionEnum.Create,
              Next = new TransactionOrderModel
              {
                Size = 1,
                Type = TransactionTypeEnum.Buy,
                Instrument = instrumentX
              }
            });
          }

          if (indicatorX < indicatorY)
          {
            gateway.OrderSenderStream.OnNext(new TransactionMessage<ITransactionOrderModel>
            {
              Action = ActionEnum.Create,
              Next = new TransactionOrderModel
              {
                Size = 1,
                Type = TransactionTypeEnum.Buy,
                Instrument = instrumentX
              }
            });

            gateway.OrderSenderStream.OnNext(new TransactionMessage<ITransactionOrderModel>
            {
              Action = ActionEnum.Create,
              Next = new TransactionOrderModel
              {
                Size = 1,
                Type = TransactionTypeEnum.Sell,
                Instrument = instrumentY
              }
            });
          }
        }

        if (account.ActivePositions.Any() && Math.Abs(indicatorX.Value - indicatorY.Value) < 0.05)
        {
        }
      }
    }

    /// <summary>
    /// Define what gateways will be used
    /// </summary>
    protected void CreateGateways(IGatewayModel gateway)
    {
      Gateways.Add(gateway);
    }

    /// <summary>
    /// Define what entites will be displayed on the chart
    /// </summary>
    protected void CreateCharts(IInstrumentModel instrumentX, IInstrumentModel instrumentY)
    {
      instrumentX.Chart.Name = _assetX;
      instrumentX.Chart.ChartArea = _assetX;
      instrumentX.Chart.ChartType = nameof(ChartTypeEnum.Candle);

      instrumentY.Chart.Name = _assetY;
      instrumentY.Chart.ChartArea = _assetY;
      instrumentY.Chart.ChartType = nameof(ChartTypeEnum.Candle);

      _scaleIndicatorX.Chart.Name = _scaleIndicatorX.Name;
      _scaleIndicatorX.Chart.ChartArea = "Indicators";
      _scaleIndicatorX.Chart.ChartType = nameof(ChartTypeEnum.Line);

      _scaleIndicatorY.Chart.Name = _scaleIndicatorY.Name;
      _scaleIndicatorY.Chart.ChartArea = "Indicators";
      _scaleIndicatorY.Chart.ChartType = nameof(ChartTypeEnum.Line);

      _performanceIndicator.Chart.Name = _account;
      _performanceIndicator.Chart.ChartArea = "Performance";
      _performanceIndicator.Chart.ChartType = nameof(ChartTypeEnum.Area);

      Charts.Add(instrumentX.Chart);
      Charts.Add(instrumentY.Chart);
      Charts.Add(_scaleIndicatorX.Chart);
      Charts.Add(_scaleIndicatorY.Chart);
      Charts.Add(_performanceIndicator.Chart);
    }
  }
}
