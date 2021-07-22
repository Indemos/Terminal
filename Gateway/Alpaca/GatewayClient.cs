using Alpaca.Markets;
using Core.EnumSpace;
using Core.ManagerSpace;
using Core.ModelSpace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Gateway.Alpaca
{
  /// <summary>
  /// Implementation
  /// </summary>
  public class GatewayClient : GatewayModel, IGatewayModel
  {
    /// <summary>
    /// HTTP client
    /// </summary>
    protected IRemoteService _serviceClient = null;

    /// <summary>
    /// Data client
    /// </summary>
    protected IAlpacaDataClient _dataClient = null;

    /// <summary>
    /// Trade client
    /// </summary>
    protected IAlpacaTradingClient _executionClient = null;

    /// <summary>
    /// Subscriptions client
    /// </summary>
    protected IAlpacaDataStreamingClient _streamClient = null;

    /// <summary>
    /// Data subscriptions
    /// </summary>
    protected IList<IAlpacaDataSubscription<IStreamQuote>> _dataStreams = new List<IAlpacaDataSubscription<IStreamQuote>>();

    /// <summary>
    /// Trade subscriptions
    /// </summary>
    protected IList<IAlpacaDataSubscription<IStreamTrade>> _executionStreams = new List<IAlpacaDataSubscription<IStreamTrade>>();

    /// <summary>
    /// API key
    /// </summary>
    public string Token { get; set; }

    /// <summary>
    /// API secret
    /// </summary>
    public string Secret { get; set; }

    /// <summary>
    /// Production or Sandbox
    /// </summary>
    public EnvironmentEnum Mode { get; set; } = EnvironmentEnum.Development;

    /// <summary>
    /// Establish connection with a server
    /// </summary>
    /// <param name="docHeader"></param>
    public override Task Connect()
    {
      return Task.Run(async () =>
      {
        try
        {
          await Disconnect();

          OrderSenderStream
            .TakeUntil(_subscriptions)
            .Subscribe(message =>
            {
              switch (message.Action)
              {
                case ActionEnum.Create: CreateOrder(message.Next); break;
                case ActionEnum.Update: UpdateOrder(message.Next); break;
                case ActionEnum.Delete: DeleteOrder(message.Next); break;
              }
            });

          switch (Mode)
          {
            case EnvironmentEnum.Production:

              _dataClient = Environments.Live.GetAlpacaDataClient(new SecretKey(Token, Secret));
              _executionClient = Environments.Live.GetAlpacaTradingClient(new SecretKey(Token, Secret));
              _streamClient = Environments.Live.GetAlpacaDataStreamingClient(new SecretKey(Token, Secret));

              break;

            case EnvironmentEnum.Development:

              _dataClient = Environments.Paper.GetAlpacaDataClient(new SecretKey(Token, Secret));
              _executionClient = Environments.Paper.GetAlpacaTradingClient(new SecretKey(Token, Secret));
              _streamClient = Environments.Paper.GetAlpacaDataStreamingClient(new SecretKey(Token, Secret));

              break;
          }

          var account = await _executionClient.GetAccountAsync();
          var orders = await _executionClient.ListOrdersAsync(new ListOrdersRequest());
          var positions = await _executionClient.ListPositionsAsync();

          // Account

          Account.Leverage = account.Multiplier;
          Account.Currency = ConversionManager.Enum<CurrencyEnum>(account.Currency);
          Account.Balance = ConversionManager.Value<double>(account.Equity);
          Account.InitialBalance = ConversionManager.Value<double>(account.LastEquity);

          // Orders

          foreach (var o in orders)
          {
            var order = new TransactionOrderModel();

            order.Size = o.Quantity;
            order.Time = o.CreatedAtUtc;
            order.Id = o.OrderId.ToString();
            order.Price = GetOrderPrice(o);
            order.Status = GetOrderStatus(o.OrderStatus);
            order.TimeSpan = GetOrderTimeSpan(o.TimeInForce);
            order.Type = GetOrderType(o.OrderType, o.OrderSide);
            order.Instrument = new InstrumentModel
            {
              Name = o.Symbol
            };

            Account.ActiveOrders.Add(order);
          }

          // Positions

          foreach (var o in positions)
          {
            var position = new TransactionPositionModel();
            var direction = GetPositionDirection(o.Side);

            position.Size = o.Quantity;
            position.Type = GetPositionType(o.Side);
            position.OpenPrice = ConversionManager.Value<double>(o.AverageEntryPrice);
            position.GainLoss = ConversionManager.Value<double>(o.UnrealizedProfitLoss);
            position.GainLossPoints = ConversionManager.Value<double>((o.AssetCurrentPrice - o.AverageEntryPrice)) * direction;
            position.Instrument = new InstrumentModel
            {
              Name = o.Symbol
            };

            position.OpenPrices = new List<ITransactionOrderModel>
            {
              new TransactionOrderModel
              {
                Price = position.OpenPrice,
                Instrument = position.Instrument
              }
            };

            Account.ActivePositions.Add(position);
          }

          await Subscribe();
        }
        catch (Exception e)
        {
          InstanceManager<LogService>.Instance.Log.Error(e.ToString());
        }
      });
    }

    /// <summary>
    /// Dispose environment
    /// </summary>
    /// <returns></returns>
    public override Task Disconnect()
    {
      _subscriptions.OnNext(true);

      Unsubscribe();

      _subscriptions.OnNext(false);

      _dataClient?.Dispose();
      _streamClient?.Dispose();
      _executionClient?.Dispose();
      _serviceClient?.Client?.Dispose();

      return Task.FromResult(0);
    }

    /// <summary>
    /// Start streaming
    /// </summary>
    /// <returns></returns>
    public override Task Subscribe()
    {
      _streamClient.ConnectAndAuthenticateAsync().ContinueWith(o =>
      {
        var streams = new List<IAlpacaDataSubscription>();

        foreach (var instrument in Account.Instruments)
        {
          var dataStream = _streamClient.GetQuoteSubscription(instrument.Value.Name);
          var executionStream = _streamClient.GetTradeSubscription(instrument.Value.Name);

          streams.Add(dataStream);
          streams.Add(executionStream);

          _dataStreams.Add(dataStream);
          _executionStreams.Add(executionStream);

          dataStream.Received += OnInputQuote;
          executionStream.Received += OnInputTrade;
        }

        _streamClient.Subscribe(streams);
      });

      return Task.FromResult(0);
    }

    /// <summary>
    /// Stop streaming without but keep environment state
    /// </summary>
    /// <returns></returns>
    public override Task Unsubscribe()
    {
      if (_streamClient != null)
      {
        var streams = new List<IAlpacaDataSubscription>();

        _dataStreams.ForEach(o => o.Received -= OnInputQuote);
        _executionStreams.ForEach(o => o.Received -= OnInputTrade);

        _streamClient.Unsubscribe(streams.Concat(_dataStreams).Concat(_executionStreams));
        _streamClient.DisconnectAsync();
      }

      return Task.FromResult(0);
    }

    /// <summary>
    /// Extract position type
    /// </summary>
    /// <param name="positionSide"></param>
    protected TransactionTypeEnum? GetPositionType(PositionSide positionSide)
    {
      switch (positionSide)
      {
        case PositionSide.Long: return TransactionTypeEnum.Buy;
        case PositionSide.Short: return TransactionTypeEnum.Sell;
      }

      return null;
    }

    /// <summary>
    /// Extract position direction
    /// </summary>
    /// <param name="positionSide"></param>
    protected double GetPositionDirection(PositionSide positionSide)
    {
      switch (positionSide)
      {
        case PositionSide.Long: return 1.0;
        case PositionSide.Short: return -1.0;
      }

      return 0.0;
    }

    /// <summary>
    /// Extract order price
    /// </summary>
    /// <param name="orderType"></param>
    protected double? GetOrderPrice(IOrder order)
    {
      switch (order.OrderType)
      {
        case OrderType.Stop:
        case OrderType.StopLimit:

          return ConversionManager.Value<double>(order.StopPrice);

        case OrderType.Limit:

          return ConversionManager.Value<double>(order.LimitPrice);
      }

      return null;
    }

    /// <summary>
    /// Extract time span 
    /// </summary>
    /// <param name="span"></param>
    protected OrderTimeSpanEnum? GetOrderTimeSpan(TimeInForce span)
    {
      switch (span)
      {
        case TimeInForce.Day: return OrderTimeSpanEnum.Date;
        case TimeInForce.Fok: return OrderTimeSpanEnum.FillOrKill;
        case TimeInForce.Gtc: return OrderTimeSpanEnum.GoodTillCancel;
        case TimeInForce.Ioc: return OrderTimeSpanEnum.ImmediateOrKill;
      }

      return null;
    }

    /// <summary>
    /// Extract order type
    /// </summary>
    /// <param name="orderType"></param>
    /// <param name="orderSide"></param>
    protected TransactionTypeEnum? GetOrderType(OrderType orderType, OrderSide orderSide)
    {
      switch (orderSide)
      {
        case OrderSide.Buy:

          switch (orderType)
          {
            case OrderType.Market: return TransactionTypeEnum.Buy;
            case OrderType.Stop: return TransactionTypeEnum.BuyStop;
            case OrderType.Limit: return TransactionTypeEnum.BuyLimit;
            case OrderType.StopLimit: return TransactionTypeEnum.BuyStopLimit;
          }

          break;

        case OrderSide.Sell:

          switch (orderType)
          {
            case OrderType.Market: return TransactionTypeEnum.Sell;
            case OrderType.Stop: return TransactionTypeEnum.SellStop;
            case OrderType.Limit: return TransactionTypeEnum.SellLimit;
            case OrderType.StopLimit: return TransactionTypeEnum.SellStopLimit;
          }

          break;
      }

      return null;
    }

    /// <summary>
    /// Extract order status
    /// </summary>
    /// <param name="status"></param>
    protected TransactionStatusEnum? GetOrderStatus(OrderStatus status)
    {
      switch (status)
      {
        case OrderStatus.New:
        case OrderStatus.Held:
        case OrderStatus.Accepted:
        case OrderStatus.Replaced:
        case OrderStatus.Suspended:
        case OrderStatus.Calculated:
        case OrderStatus.PendingNew:
        case OrderStatus.DoneForDay:
        case OrderStatus.PendingReplace:
        case OrderStatus.AcceptedForBidding:

          return TransactionStatusEnum.Placed;

        case OrderStatus.Canceled:
        case OrderStatus.PendingCancel:

          return TransactionStatusEnum.Cancelled;

        case OrderStatus.Expired:

          return TransactionStatusEnum.Expired;

        case OrderStatus.Fill:
        case OrderStatus.Filled:

          return TransactionStatusEnum.Filled;

        case OrderStatus.PartialFill:
        case OrderStatus.PartiallyFilled:

          return TransactionStatusEnum.PartiallyFilled;

        case OrderStatus.Stopped:
        case OrderStatus.Rejected:

          return TransactionStatusEnum.Declined;
      }

      return null;
    }

    /// <summary>
    /// Place new order
    /// </summary>
    /// <param name="orders"></param>
    /// <returns></returns>
    public virtual Task<IEnumerable<ITransactionOrderModel>> CreateOrder(params ITransactionOrderModel[] orders)
    {
      return Task.FromResult(orders as IEnumerable<ITransactionOrderModel>);
    }

    /// <summary>
    /// Update order
    /// </summary>
    /// <param name="orders"></param>
    /// <returns></returns>
    public virtual Task<IEnumerable<ITransactionOrderModel>> UpdateOrder(params ITransactionOrderModel[] orders)
    {
      return Task.FromResult(orders as IEnumerable<ITransactionOrderModel>);
    }

    /// <summary>
    /// Cancel order
    /// </summary>
    /// <param name="orders"></param>
    /// <returns></returns>
    public virtual Task<IEnumerable<ITransactionOrderModel>> DeleteOrder(params ITransactionOrderModel[] orders)
    {
      return Task.FromResult(orders as IEnumerable<ITransactionOrderModel>);
    }

    /// <summary>
    /// Process incoming quotes
    /// </summary>
    /// <param name="input"></param>
    protected void OnInputQuote(IStreamQuote input)
    {
      var point = new PointModel
      {
        Time = input.TimeUtc,
        AskSize = input.AskSize,
        BidSize = input.BidSize,
        Bar = new PointBarModel(),
        Instrument = Account.Instruments[input.Symbol],
        Ask = ConversionManager.Value<double>(input.AskPrice),
        Bid = ConversionManager.Value<double>(input.BidPrice)
      };

      UpdatePointParams(point, Account.Instruments[input.Symbol]);
    }

    /// <summary>
    /// Process incoming quotes
    /// </summary>
    /// <param name="input"></param>
    protected void OnInputTrade(IStreamTrade input)
    {
    }
  }
}
