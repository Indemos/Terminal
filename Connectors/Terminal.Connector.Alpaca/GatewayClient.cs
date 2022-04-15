using Core.EnumSpace;
using Core.ManagerSpace;
using Core.ModelSpace;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Websocket.Client;

namespace Gateway.Alpaca
{
  /// <summary>
  /// Implementation
  /// </summary>
  public class GatewayClient : GatewayModel, IGatewayModel
  {
    /// <summary>
    /// API key
    /// </summary>
    public string Token { get; set; }

    /// <summary>
    /// API secret
    /// </summary>
    public string Secret { get; set; }

    /// <summary>
    /// Data source
    /// </summary>
    public string DataSource { get; set; } = "https://data.alpaca.markets";

    /// <summary>
    /// Data source
    /// </summary>
    public string QuerySource { get; set; } = "https://api.alpaca.markets";

    /// <summary>
    /// Stream source
    /// </summary>
    public string StreamSource { get; set; } = "wss://data.alpaca.markets/stream";

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

          _serviceClient = new ClientService();
          _serviceClient.Client.DefaultRequestHeaders.Add("Accept", "application/json");
          _serviceClient.Client.DefaultRequestHeaders.Add("APCA-API-KEY-ID", Token);
          _serviceClient.Client.DefaultRequestHeaders.Add("APCA-API-SECRET-KEY", Secret);

          _connections.Add(_serviceClient);

          //await GetAccountData();
          //await GetActiveOrders();
          //await GetActivePositions();
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
      Unsubscribe();

      _connections.ForEach(o => o.Dispose());
      _connections.Clear();

      return Task.FromResult(0);
    }

    /// <summary>
    /// Start streaming
    /// </summary>
    /// <returns></returns>
    public override async Task Subscribe()
    {
      await Unsubscribe();

      // Orders

      var subscription = OrderSenderStream.Subscribe(message =>
      {
        switch (message.Action)
        {
          case ActionEnum.Create: CreateOrders(message.Next); break;
          case ActionEnum.Update: UpdateOrders(message.Next); break;
          case ActionEnum.Delete: DeleteOrders(message.Next); break;
        }
      });

      _subscriptions.Add(subscription);

      // Streaming

      var client = new WebsocketClient(new Uri(StreamSource), _streamOptions)
      {
        Name = Account.Name,
        ReconnectTimeout = TimeSpan.FromSeconds(30),
        ErrorReconnectTimeout = TimeSpan.FromSeconds(30)
      };

      var connectionSubscription = client.ReconnectionHappened.Subscribe(message => { });
      var disconnectionSubscription = client.DisconnectionHappened.Subscribe(message => { });
      var messageSubscription = client.MessageReceived.Subscribe(message =>
      {
        dynamic input = JObject.Parse(message.Text);

        var inputStream = $"{ input.stream }";

        if (inputStream.StartsWith("Q."))
        {
          OnInputQuote(input.data);
        }

        if (inputStream.StartsWith("T."))
        {
          OnInputTrade(input.data);
        }

        if (Equals(inputStream, "authorization") && Equals($"{ input.data.status }", "authorized"))
        {
          var subscriptions = Account
            .Instruments
            .SelectMany(o => new[]
            {
              "T." + o.Value.Name,
              "Q." + o.Value.Name
            });

          var query = new
          {
            action = "listen",
            data = new
            {
              streams = subscriptions
            }
          };

          client.Send(ConversionManager.Serialize(query));
        }
      });

      _subscriptions.Add(messageSubscription);
      _subscriptions.Add(connectionSubscription);
      _subscriptions.Add(disconnectionSubscription);

      await client.Start();

      var query = new
      {
        action = "authenticate",
        data = new
        {
          key_id = Token,
          secret_key = Secret
        }
      };

      client.Send(ConversionManager.Serialize(query));
    }

    /// <summary>
    /// Stop streaming without but keep environment state
    /// </summary>
    /// <returns></returns>
    public override Task Unsubscribe()
    {
      _subscriptions.ForEach(o => o.Dispose());
      _subscriptions.Clear();

      return Task.FromResult(0);
    }

    /// <summary>
    /// Place new order
    /// </summary>
    /// <param name="orders"></param>
    /// <returns></returns>
    public override Task<IEnumerable<ITransactionOrderModel>> CreateOrders(params ITransactionOrderModel[] orders)
    {
      return Task.Run(() =>
      {
        foreach (var nextOrder in orders)
        {
          //await CreateOrder(nextOrder);
        }

        return orders as IEnumerable<ITransactionOrderModel>;
      });
    }

    /// <summary>
    /// Update order
    /// </summary>
    /// <param name="orders"></param>
    /// <returns></returns>
    public override Task<IEnumerable<ITransactionOrderModel>> UpdateOrders(params ITransactionOrderModel[] orders)
    {
      return Task.FromResult(orders as IEnumerable<ITransactionOrderModel>);
    }

    /// <summary>
    /// Cancel order
    /// </summary>
    /// <param name="orders"></param>
    /// <returns></returns>
    public override Task<IEnumerable<ITransactionOrderModel>> DeleteOrders(params ITransactionOrderModel[] orders)
    {
      return Task.FromResult(orders as IEnumerable<ITransactionOrderModel>);
    }

    /// <summary>
    /// Convert abstract order to a real one
    /// </summary>
    /// <param name="internalOrder"></param>
    /// <param name="orderClass"></param>
    /// <returns></returns>
    //protected async Task<ITransactionOrderModel> CreateOrder(ITransactionOrderModel internalOrder, dynamic orderClass = null)
    //{
    //  var size = ConversionManager.To<long>(internalOrder.Size);
    //  var price = ConversionManager.To<decimal>(internalOrder.Price);
    //  var span = MapInput.GetTimeSpan(internalOrder.TimeSpan.Value).Value;
    //  var orderSide = MapInput.GetOrderSide(internalOrder.Side.Value).Value;
    //  var orderType = MapInput.GetOrderType(internalOrder.Type.Value).Value;
    //  var activationPrice = ConversionManager.To<decimal>(internalOrder.ActivationPrice);
    //  var orderQuery = new NewOrderRequest(internalOrder.Instrument.Name, size, orderSide, orderType, span)
    //  {
    //    ClientOrderId = internalOrder.Id
    //  };

    //  if (orderClass != null)
    //  {
    //    orderQuery.OrderClass = orderClass;
    //  }

    //  switch (orderType)
    //  {
    //    case OrderType.Stop: orderQuery.StopPrice = price; break;
    //    case OrderType.Limit: orderQuery.LimitPrice = price; break;
    //    case OrderType.StopLimit: orderQuery.StopPrice = activationPrice; orderQuery.LimitPrice = price; break;
    //  }

    //  await _executionClient.PostOrderAsync(orderQuery);

    //  foreach (var childData in internalOrder.Orders)
    //  {
    //    await CreateOrder(childData);
    //  }

    //  return internalOrder;
    //}

    /// <summary>
    /// Load account data
    /// </summary>
    /// <returns></returns>
    //protected async Task GetAccountData()
    //{
    //  var account = await _executionClient.GetAccountAsync();

    //  Account.Leverage = account.Multiplier;
    //  Account.Currency = account.Currency.ToUpper();
    //  Account.Balance = ConversionManager.To<double>(account.Equity);
    //  Account.InitialBalance = ConversionManager.To<double>(account.LastEquity);
    //}

    /// <summary>
    /// Load orders
    /// </summary>
    /// <returns></returns>
    //protected async Task GetOrders()
    //{
    //  var orders = await _executionClient.ListOrdersAsync(new ListOrdersRequest
    //  {
    //    LimitOrderNumber = 500,
    //    OrderListSorting = SortDirection.Descending,
    //    OrderStatusFilter = OrderStatusFilter.Closed
    //  });

    //  foreach (var o in orders)
    //  {
    //    var order = new TransactionOrderModel();

    //    Account.Instruments.TryGetValue(o.Symbol, out IInstrumentModel instrument);

    //    order.Size = o.Quantity;
    //    order.Time = o.CreatedAtUtc;
    //    order.Id = o.OrderId.ToString();
    //    order.Type = MapOutput.GetOrderType(o.OrderType);
    //    order.Side = MapOutput.GetOrderSide(o.OrderSide);
    //    order.Status = MapOutput.GetOrderStatus(o.OrderStatus);
    //    order.TimeSpan = MapOutput.GetTimeSpan(o.TimeInForce);
    //    order.Price = ConversionManager.To<double>(o.AverageFillPrice ?? o.StopPrice ?? o.LimitPrice);
    //    order.Instrument = instrument ?? new InstrumentModel
    //    {
    //      Name = o.Symbol
    //    };

    //    Account.Orders.Add(order);
    //  }
    //}

    /// <summary>
    /// Load active orders
    /// </summary>
    /// <returns></returns>
    //protected async Task GetActiveOrders()
    //{
    //  var orders = await _executionClient.ListOrdersAsync(new ListOrdersRequest
    //  {
    //    LimitOrderNumber = 500,
    //    OrderListSorting = SortDirection.Descending,
    //    OrderStatusFilter = OrderStatusFilter.Open
    //  });

    //  foreach (var o in orders)
    //  {
    //    var order = new TransactionOrderModel();

    //    Account.Instruments.TryGetValue(o.Symbol, out IInstrumentModel instrument);

    //    order.Size = o.Quantity;
    //    order.Time = o.CreatedAtUtc;
    //    order.Id = o.OrderId.ToString();
    //    order.Type = MapOutput.GetOrderType(o.OrderType);
    //    order.Side = MapOutput.GetOrderSide(o.OrderSide);
    //    order.Status = MapOutput.GetOrderStatus(o.OrderStatus);
    //    order.TimeSpan = MapOutput.GetTimeSpan(o.TimeInForce);
    //    order.Price = ConversionManager.To<double>(o.AverageFillPrice ?? o.StopPrice ?? o.LimitPrice);
    //    order.Instrument = instrument ?? new InstrumentModel
    //    {
    //      Name = o.Symbol
    //    };

    //    Account.ActiveOrders.Add(order);
    //  }
    //}

    /// <summary>
    /// Load active positions
    /// </summary>
    /// <returns></returns>
    //protected async Task GetActivePositions()
    //{
    //  var positions = await _executionClient.ListPositionsAsync();

    //  foreach (var o in positions)
    //  {
    //    var position = new TransactionPositionModel();

    //    Account.Instruments.TryGetValue(o.Symbol, out IInstrumentModel instrument);

    //    position.Size = o.Quantity;
    //    position.Time = DateTime.MinValue;
    //    position.Type = OrderTypeEnum.Market;
    //    position.Side = MapOutput.GetPositionSide(o.Side);
    //    position.OpenPrice = ConversionManager.To<double>(o.AverageEntryPrice);
    //    position.ClosePrice = ConversionManager.To<double>(o.AssetCurrentPrice);
    //    position.GainLoss = ConversionManager.To<double>(o.UnrealizedProfitLoss);
    //    position.GainLossPoints = ConversionManager.To<double>((o.AssetCurrentPrice - o.AverageEntryPrice)) * MapOutput.GetDirection(position.Side.Value);
    //    position.Instrument = instrument ?? new InstrumentModel
    //    {
    //      Name = o.Symbol
    //    };

    //    position.OpenPrices = new List<ITransactionOrderModel>
    //    {
    //      new TransactionOrderModel
    //      {
    //        Price = position.OpenPrice,
    //        Instrument = position.Instrument
    //      }
    //    };

    //    Account.ActivePositions.Add(position);
    //  }
    //}

    /// <summary>
    /// Process incoming quotes
    /// </summary>
    /// <param name="input"></param>
    protected void OnInputQuote(dynamic input)
    {
      var currentAsk = ConversionManager.To<double>(input.P);
      var currentBid = ConversionManager.To<double>(input.p);
      var previousAsk = _point?.Ask ?? currentAsk;
      var previousBid = _point?.Bid ?? currentBid;
      var symbol = $"{ input.T }";

      var point = new PointModel
      {
        Ask = currentAsk,
        Bid = currentBid,
        Bar = new PointBarModel(),
        Instrument = Account.Instruments[symbol],
        AskSize = ConversionManager.To<double>(input.S),
        BidSize = ConversionManager.To<double>(input.s),
        Time = _unixTime.AddTicks(ConversionManager.To<long>(input.t) / 100),
        Last = ConversionManager.Compare(currentBid, previousBid) ? currentAsk : currentBid
      };

      _point = point;

      UpdatePointProps(point);
    }

    /// <summary>
    /// Process incoming quotes
    /// </summary>
    /// <param name="input"></param>
    protected void OnInputTrade(dynamic input)
    {
    }
  }
}
