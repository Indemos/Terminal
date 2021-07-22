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
  public class ImbalanceStrategy : BaseStrategy
  {
    const string _asset = "GOOG";
    const string _account = "Simulation";

    protected DateTime? _date = null;
    protected TimeSpan? _span = null;
    protected IInstrumentModel _instrument = null;
    protected ImbalanceIndicator _imbalanceIndicator = null;
    protected PerformanceIndicator _performanceIndicator = null;

    public override Task OnLoad()
    {
      _date = DateTime.MinValue;
      _span = TimeSpan.FromMinutes(1);

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
        Account = account,
        Evaluate = Parse,
        Source = Startup.Configuration.GetValue<string>("Gateway:DataLocation")
      };

      _imbalanceIndicator = new ImbalanceIndicator { Name = "Imbalance" };
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
      var date = point.Time;
      var account = point.Account;
      var gateway = account.Gateway;
      var instrument = point.Account.Instruments[_asset];
      var series = instrument.PointGroups;
      var imbalanceIndicator = _imbalanceIndicator.Calculate(series);
      var performanceIndicator = _performanceIndicator.Calculate(series, Gateways.Select(o => o.Account));
      var volumes = imbalanceIndicator.Values;

      if (series.Any() && volumes.Count > 2 && point.Time.Value.Minute != _date.Value.Minute)
      {
        var noOrders = account.ActiveOrders.Any() == false;
        var noPositions = account.ActivePositions.Any() == false;
        var currentSize = volumes.ElementAt(volumes.Count - 2).Bar.Close;
        var previousSize = volumes.ElementAt(volumes.Count - 3).Bar.Close;
        var isNextStep = date.Value.Minute != _date.Value.Minute;
        var isLongVolume = currentSize > 0 && isNextStep && currentSize.Value > previousSize.Value && previousSize.Value > 0;
        var isShortVolume = currentSize < 0 && isNextStep && currentSize.Value < previousSize.Value && previousSize.Value < 0;

        if (noOrders && noPositions)
        {
          if (isLongVolume) CreateOrder(point, TransactionTypeEnum.Buy, 1);
          if (isShortVolume) CreateOrder(point, TransactionTypeEnum.Sell, 1);
        }

        if (noPositions == false)
        {
          var activePosition = account.ActivePositions.Last();

          switch (activePosition.Type)
          {
            case TransactionTypeEnum.Buy:

              if (isLongVolume) CreateOrder(point, TransactionTypeEnum.Buy, 1);
              if (isShortVolume) CreateOrder(point, TransactionTypeEnum.Sell, activePosition.Size.Value + 1);

              break;

            case TransactionTypeEnum.Sell:

              if (isShortVolume) CreateOrder(point, TransactionTypeEnum.Sell, 1);
              if (isLongVolume) CreateOrder(point, TransactionTypeEnum.Buy, activePosition.Size.Value + 1);

              break;
          }
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

      _date = point.Time;

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

      _imbalanceIndicator.Chart.Name = _imbalanceIndicator.Name;
      _imbalanceIndicator.Chart.ChartArea = "Imbalance";
      _imbalanceIndicator.Chart.ChartType = nameof(ChartTypeEnum.Bar);

      _performanceIndicator.Chart.Name = _performanceIndicator.Name;
      _performanceIndicator.Chart.ChartArea = "Performance";
      _performanceIndicator.Chart.ChartType = nameof(ChartTypeEnum.Area);

      Charts.Add(_instrument.Chart);
      Charts.Add(_imbalanceIndicator.Chart);
      Charts.Add(_performanceIndicator.Chart);
    }
  }
}
