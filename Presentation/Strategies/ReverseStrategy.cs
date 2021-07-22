using Core.CollectionSpace;
using Core.EnumSpace;
using Core.IndicatorSpace;
using Core.MessageSpace;
using Core.ModelSpace;
using Gateway.Simulation;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Presentation.StrategySpace
{
  public class ReverseStrategy : BaseStrategy
  {
    const string _asset = "GOOG";
    const string _account = "Simulation";

    protected TimeSpan _span = TimeSpan.FromMinutes(1);

    protected IInstrumentModel _instrument = null;
    protected MovingAverageIndicator _bidIndicator = null;
    protected MovingAverageIndicator _askIndicator = null;
    protected RelativeStrengthIndicator _rsiIndicator = null;
    protected AverageTrueRangeIndicator _atrIndicator = null;
    protected PerformanceIndicator _performanceIndicator = null;

    public override Task OnLoad()
    {
      _instrument = new InstrumentModel
      {
        Name = _asset,
        TimeFrame = _span
      };

      var account = new AccountModel
      {
        Name = _account,
        Balance = 50000,
        InitialBalance = 50000,
        Instruments = new NameCollection<string, IInstrumentModel> { [_asset] = _instrument }
      };

      var gateway = new GatewayClient
      {
        Name = _account,
        Speed = 10,
        Account = account,
        Evaluate = Parse,
        Source = Startup.Configuration.GetValue<string>("Gateway:DataLocation")
      };

      _rsiIndicator = new RelativeStrengthIndicator { Interval = 10, Name = "RSI Indicator : " + _asset };
      _atrIndicator = new AverageTrueRangeIndicator { Interval = 10, Name = "ATR Indicator : " + _asset };
      _bidIndicator = new MovingAverageIndicator { Interval = 0, Mode = MovingAverageEnum.Bid, Name = "BID Indicator : " + _asset };
      _askIndicator = new MovingAverageIndicator { Interval = 0, Mode = MovingAverageEnum.Ask, Name = "ASK Indicator : " + _asset };
      _performanceIndicator = new PerformanceIndicator { Name = "Balance" };

      gateway
        .Account
        .Instruments
        .Values
        .Select(o => o.PointGroups.ItemStream)
        .Merge()
        .TakeUntil(_subscriptions)
        .Subscribe(OnData);

      CreateCharts();
      CreateGateways(gateway);

      return Task.FromResult(0);
    }

    protected void OnData(ITransactionMessage<IPointModel> message)
    {
      var point = message.Next;
      var account = point.Account;
      var gateway = account.Gateway;
      var instrument = point.Account.Instruments[_asset];
      var series = instrument.PointGroups;
      var bidIndicator = _bidIndicator.Calculate(series);
      var askIndicator = _askIndicator.Calculate(series);
      var rsiIndicator = _rsiIndicator.Calculate(series);
      var atrIndicator = _atrIndicator.Calculate(series);
      var performanceIndicator = _performanceIndicator.Calculate(series, Gateways.Select(o => o.Account));

      if (series.Any())
      {
        var rsiValues = rsiIndicator.Values;
        var noOrders = account.ActiveOrders.Any() == false;
        var noPositions = account.ActivePositions.Any() == false;

        if (noOrders && noPositions && rsiValues.Count > 1)
        {
          var rsiCurrent = rsiValues.ElementAt(rsiValues.Count - 1).Bar.Close;
          var rsiPrevious = rsiValues.ElementAt(rsiValues.Count - 2).Bar.Close;

          if (rsiPrevious < 30 && rsiCurrent > 30) CreateOrder(point, TransactionTypeEnum.Buy, 100);
          if (rsiPrevious > 70 && rsiCurrent < 70) CreateOrder(point, TransactionTypeEnum.Sell, 100);
        }

        if (noPositions == false)
        {
          var activePosition = account.ActivePositions.Last();
          var rsiCurrent = rsiValues.ElementAt(rsiValues.Count - 1).Bar.Close;
          var rsiPrevious = rsiValues.ElementAt(rsiValues.Count - 2).Bar.Close;

          if (Equals(activePosition.Type, TransactionTypeEnum.Buy) && rsiPrevious > 70 && rsiCurrent < 70) CreateOrder(point, TransactionTypeEnum.Sell, 200);
          if (Equals(activePosition.Type, TransactionTypeEnum.Sell) && rsiPrevious < 30 && rsiCurrent > 30) CreateOrder(point, TransactionTypeEnum.Buy, 200);
        }
      }
    }

    /// <summary>
    /// Helper method to send orders
    /// </summary>
    /// <param name="point"></param>
    /// <param name="side"></param>
    /// <param name="size"></param>
    /// <returns></returns>
    protected ITransactionOrderModel CreateOrder(IPointModel point, TransactionTypeEnum side, double size)
    {
      var gateway = point.Account.Gateway;
      var instrument = point.Account.Instruments[_asset];
      var order = new TransactionOrderModel
      {
        Size = size,
        Type = side,
        Instrument = instrument
      };

      gateway.OrderSenderStream.OnNext(new TransactionMessage<ITransactionOrderModel>
      {
        Action = ActionEnum.Create,
        Next = order
      });

      return order;
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
    protected void CreateCharts()
    {
      _instrument.Chart.Name = _asset;
      _instrument.Chart.ChartArea = _asset;
      _instrument.Chart.ChartType = nameof(ChartTypeEnum.Candle);

      _bidIndicator.Chart.Name = _bidIndicator.Name;
      _bidIndicator.Chart.ChartArea = _asset;
      _bidIndicator.Chart.ChartType = nameof(ChartTypeEnum.Line);

      _askIndicator.Chart.Name = _askIndicator.Name;
      _askIndicator.Chart.ChartArea = _asset;
      _askIndicator.Chart.ChartType = nameof(ChartTypeEnum.Line);

      _rsiIndicator.Chart.Name = _rsiIndicator.Name;
      _rsiIndicator.Chart.ChartArea = "RSI";
      _rsiIndicator.Chart.ChartType = nameof(ChartTypeEnum.Line);

      _atrIndicator.Chart.Name = _atrIndicator.Name;
      _atrIndicator.Chart.ChartArea = "ATR";
      _atrIndicator.Chart.ChartType = nameof(ChartTypeEnum.Line);

      _performanceIndicator.Chart.Name = _performanceIndicator.Name;
      _performanceIndicator.Chart.ChartArea = "Performance";
      _performanceIndicator.Chart.ChartType = nameof(ChartTypeEnum.Area);

      Charts.Add(_instrument.Chart);
      Charts.Add(_bidIndicator.Chart);
      Charts.Add(_askIndicator.Chart);
      Charts.Add(_rsiIndicator.Chart);
      Charts.Add(_atrIndicator.Chart);
      Charts.Add(_performanceIndicator.Chart);
    }
  }
}
