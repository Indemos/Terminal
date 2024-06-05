using Alpaca.Markets;
using Distribution.Services;
using Distribution.Stream;
using Distribution.Stream.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Extensions;
using Terminal.Core.Models;
using Terminal.Core.Services;

namespace Alpaca
{
  public class Adapter : Gateway
  {
    /// <summary>
    /// HTTP client
    /// </summary>
    protected Service _sender;

    /// <summary>
    /// Socket connection
    /// </summary>
    protected ClientWebSocket _streamer;

    /// <summary>
    /// Disposable connections
    /// </summary>
    protected IList<IDisposable> _connections;

    /// <summary>
    /// Disposable subscriptions
    /// </summary>
    protected IList<IDisposable> _subscriptions;

    /// <summary>
    /// Key
    /// </summary>
    public virtual string ConsumerKey { get; set; }

    /// <summary>
    /// Secret
    /// </summary>
    public virtual string ConsumerSecret { get; set; }

    /// <summary>
    /// Data source
    /// </summary>
    public virtual string DataUri { get; set; }

    /// <summary>
    /// Streaming source
    /// </summary>
    public virtual string StreamUri { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public Adapter()
    {
      StreamUri = "wss://stream.data.alpaca.markets/v2/iex";
      DataUri = "https://api.alpaca.markets";

      _subscriptions = [];
      _connections = [];
    }

    /// <summary>
    /// Connect
    /// </summary>
    public override async Task<IList<ErrorModel>> Connect()
    {
      var ws = new ClientWebSocket();
      var scheduler = new ScheduleService();

      await Disconnect();

      _sender = new Service();
      _streamer = await GetConnection(ws, scheduler);

      await SendStream(_streamer, new AuthenticationMessage
      {
        Action = "auth",
        SecretKey = ConsumerSecret,
        KeyId = ConsumerKey
      });

      await GetAccountData();

      _connections.Add(_sender);
      _connections.Add(_streamer);
      _connections.Add(scheduler);

      Account.Instruments.ForEach(async o => await Subscribe(o.Key));

      return null;
    }

    /// <summary>
    /// Subscribe to data streams
    /// </summary>
    public override async Task<IList<ErrorModel>> Subscribe(string name)
    {
      await Unsubscribe(name);
      await SendStream(_streamer, new SubscriptionUpdateMessage
      {
        Action = "subscribe",
        Trades = [name],
        Quotes = [name]
      });

      return null;
    }

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public override Task<IList<ErrorModel>> Disconnect()
    {
      _connections?.ForEach(o => o?.Dispose());
      _connections?.Clear();

      return Task.FromResult<IList<ErrorModel>>(null);
    }

    /// <summary>
    /// Unsubscribe from data streams
    /// </summary>
    public override async Task<IList<ErrorModel>> Unsubscribe(string name)
    {
      await SendStream(_streamer, new SubscriptionUpdateMessage
      {
        Action = "unsubscribe",
        Trades = [name],
        Quotes = [name]
      });

      _subscriptions?.ForEach(o => o.Dispose());
      _subscriptions?.Clear();

      return null;
    }

    /// <summary>
    /// Get option chains
    /// </summary>
    /// <param name="message"></param>
    public override Task<ResponseItemModel<IList<OptionModel>>> GetOptions(OptionMessageModel message)
    {
      throw new NotImplementedException();
    }

    /// <summary>
    /// Quotes
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public override Task<ResponseItemModel<IList<PointModel>>> GetPoints(PointMessageModel message)
    {
      var response = new ResponseItemModel<IList<PointModel>>
      {
        Data = Account.Instruments[message.Name].Points
      };

      return Task.FromResult(response);
    }

    /// <summary>
    /// Send orders
    /// </summary>
    /// <param name="orders"></param>
    /// <returns></returns>
    public override async Task<ResponseMapModel<OrderModel>> CreateOrders(params OrderModel[] orders)
    {
      var response = new ResponseMapModel<OrderModel>();

      foreach (var order in orders)
      {
        response.Items.Add(await CreateOrder(order));
      }

      return response;
    }

    /// <summary>
    /// Cancel orders
    /// </summary>
    /// <param name="orders"></param>
    /// <returns></returns>
    public override async Task<ResponseMapModel<OrderModel>> DeleteOrders(params OrderModel[] orders)
    {
      var response = new ResponseMapModel<OrderModel>();

      foreach (var order in orders)
      {
        response.Items.Add(await DeleteOrder(order));
      }

      return response;
    }

    /// <summary>
    /// Sync open balance, order, and positions 
    /// </summary>
    /// <returns></returns>
    protected async Task GetAccountData()
    {
      var account = await SendData<AccountMessage>("/v2/account");
      var positions = await SendData<PositionMessage[]>("/v2/positions");
      var orders = await SendData<OrderMessage[]>("/v2/orders");

      try
      {
        Account.Balance = account.Data.Equity;
        Account.Descriptor = account.Data.AccountNumber;
        Account.ActiveOrders = orders.Data.Select(GetInternalOrder).ToDictionary(o => o.Transaction.Id, o => o);
        Account.ActivePositions = positions.Data.Select(GetInternalPosition).ToDictionary(o => o.Order.Transaction.Id, o => o);

        Account.ActiveOrders.ForEach(async o => await Subscribe(o.Value.Transaction.Instrument.Name));
        Account.ActivePositions.ForEach(async o => await Subscribe(o.Value.Order.Transaction.Instrument.Name));
      }
      catch (Exception e)
      {
        InstanceService<MessageService>.Instance.OnMessage($"{e}");
      }
    }

    /// <summary>
    /// Send data to web socket stream
    /// </summary>
    /// <param name="ws"></param>
    /// <param name="data"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    protected Task SendStream(ClientWebSocket ws, object data, CancellationTokenSource cancellation = null)
    {
      var content = JsonSerializer.Serialize(data, _sender.Options);
      var message = Encoding.ASCII.GetBytes(content);

      return ws.SendAsync(
        message,
        WebSocketMessageType.Binary,
        true,
        cancellation?.Token ?? CancellationToken.None);
    }

    /// <summary>
    /// Web socket stream
    /// </summary>
    /// <param name="ws"></param>
    /// <param name="scheduler"></param>
    /// <returns></returns>
    protected async Task<ClientWebSocket> GetConnection(ClientWebSocket ws, ScheduleService scheduler)
    {
      var source = new UriBuilder(StreamUri);
      var cancellation = new CancellationTokenSource();

      await ws.ConnectAsync(source.Uri, cancellation.Token);

      scheduler.Send(async () =>
      {
        while (ws.State is WebSocketState.Open)
        {
          var data = new byte[byte.MaxValue];

          await ws.ReceiveAsync(data, cancellation.Token).ContinueWith(async o =>
          {
            var response = await o;
            var content = $"[{Encoding.ASCII.GetString(data).Trim('\0', '[', ']')}]";
            var message = JsonNode
              .Parse(content)
              ?.AsArray()
              ?.FirstOrDefault();

            switch ($"{message?["T"]}".ToUpper())
            {
              case "Q": ProcessPoint(content); break;
              case "T": ProcessTrade(content); break;
            }
          });
        }
      });

      return ws;
    }

    /// <summary>
    /// Process quote from the stream
    /// </summary>
    /// <param name="content"></param>
    protected void ProcessPoint(string content)
    {
      try
      {
        var streamPoint = JsonSerializer
          .Deserialize<HistoricalQuoteMessage[]>(content, _sender.Options)
          .FirstOrDefault();

        var instrument = Account.Instruments.Get(streamPoint.Symbol) ?? new Instrument();
        var point = new PointModel();

        point.Ask = streamPoint.AskPrice;
        point.Bid = streamPoint.BidPrice;
        point.AskSize = streamPoint.AskSize ?? 0;
        point.BidSize = streamPoint.BidSize ?? 0;
        point.Last = streamPoint.BidPrice ?? streamPoint.AskPrice;
        point.Time = streamPoint.TimestampUtc ?? DateTime.Now;
        point.TimeFrame = instrument.TimeFrame;
        point.Instrument = instrument;

        instrument.Name = streamPoint.Symbol;
        instrument.Points.Add(point);
        instrument.PointGroups.Add(point);
      }
      catch (Exception e)
      {
        InstanceService<MessageService>.Instance.OnMessage(e.Message);
      }
    }

    /// <summary>
    /// Process quote from the stream
    /// </summary>
    /// <param name="content"></param>
    protected void ProcessTrade(string content)
    {
      try
      {
        var order = JsonSerializer
          .Deserialize<HistoricalTradeMessage[]>(content, _sender.Options)
          .FirstOrDefault();
      }
      catch (Exception e)
      {
        InstanceService<MessageService>.Instance.OnMessage(e.Message);
      }
    }

    /// <summary>
    /// Send data to the API
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="verb"></param>
    /// <param name="query"></param>
    /// <returns></returns>
    protected async Task<Distribution.Stream.Models.ResponseModel<T>> SendData<T>(
      string source,
      HttpMethod verb = null,
      Hashtable query = null,
      object content = null)
    {
      query ??= [];
      verb ??= HttpMethod.Get;

      var uri = new UriBuilder(DataUri)
      {
        Path = source
      };

      var message = new HttpRequestMessage
      {
        Method = verb
      };

      switch (true)
      {
        case true when Equals(verb, HttpMethod.Get): uri.Query = query.ToQuery(); break;
        case true when Equals(verb, HttpMethod.Put):
        case true when Equals(verb, HttpMethod.Post):
        case true when Equals(verb, HttpMethod.Patch): message.Content = new StringContent(JsonSerializer.Serialize(content)); break;
      }

      message.RequestUri = uri.Uri;
      message.Headers.Add("APCA-API-KEY-ID", ConsumerKey);
      message.Headers.Add("APCA-API-SECRET-KEY", ConsumerSecret);

      return await _sender.Send<T>(message, _sender.Options);
    }

    /// <summary>
    /// Send order
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    protected async Task<ResponseItemModel<OrderModel>> CreateOrder(OrderModel order)
    {
      var inResponse = new ResponseItemModel<OrderModel>();

      try
      {
        var exOrder = GetExternalOrder(order);
        var exResponse = await SendData<OrderMessage>("/v2/orders", HttpMethod.Post, null, exOrder);

        inResponse.Data = order;
        inResponse.Data.Transaction.Id = $"{exResponse.Data.OrderId}";
        inResponse.Data.Transaction.Status = GetInternalStatus(exResponse.Data.OrderStatus);

        if (exResponse.Status < 400)
        {
          Account.ActiveOrders.Add(order.Transaction.Id, order);
        }
      }
      catch (Exception e)
      {
        inResponse.Errors.Add(new ErrorModel
        {
          ErrorMessage = e.Message
        });
      }

      return inResponse;
    }

    /// <summary>
    /// Cancel order
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    protected async Task<ResponseItemModel<OrderModel>> DeleteOrder(OrderModel order)
    {
      var inResponse = new ResponseItemModel<OrderModel>();

      try
      {
        var exResponse = await SendData<OrderMessage>($"/v2/orders/{order.Transaction.Id}", HttpMethod.Delete);

        inResponse.Data = order;
        inResponse.Data.Transaction.Id = $"{exResponse.Data.OrderId}";
        inResponse.Data.Transaction.Status = GetInternalStatus(exResponse.Data.OrderStatus);

        if (exResponse.Status < 400)
        {
          Account.ActiveOrders.Remove(order.Transaction.Id);
        }
      }
      catch (Exception e)
      {
        inResponse.Errors.Add(new ErrorModel
        {
          ErrorMessage = e.Message
        });
      }

      return inResponse;
    }

    /// <summary>
    /// Convert remote order from brokerage to local record
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    protected OrderCreationMessage GetExternalOrder(OrderModel order)
    {
      var action = order.Transaction;
      var message = new OrderCreationMessage
      {
        Quantity = action.Volume,
        Symbol = action.Instrument.Name,
        TimeInForce = GetExternalTimeSpan(order.TimeSpan.Value),
        OrderType = "market"
      };

      switch (order.Side)
      {
        case OrderSideEnum.Buy: message.OrderSide = "buy"; break;
        case OrderSideEnum.Sell: message.OrderSide = "sell"; break;
      }

      switch (order.Type)
      {
        case OrderTypeEnum.Stop: message.StopPrice = order.Price; break;
        case OrderTypeEnum.Limit: message.LimitPrice = order.Price; break;
        case OrderTypeEnum.StopLimit: message.StopPrice = order.ActivationPrice; message.LimitPrice = order.Price; break;
      }

      if (order.Orders.Any())
      {
        message.OrderClass = "bracket";

        switch (order.Side)
        {
          case OrderSideEnum.Buy:
            message.StopLoss = GetExternalBracket(order, 1);
            message.TakeProfit = GetExternalBracket(order, -1);
            break;

          case OrderSideEnum.Sell:
            message.StopLoss = GetExternalBracket(order, -1);
            message.TakeProfit = GetExternalBracket(order, 1);
            break;
        }
      }

      return null;
    }

    /// <summary>
    /// Convert local time in force to remote
    /// </summary>
    /// <param name="span"></param>
    /// <returns></returns>
    protected string GetExternalTimeSpan(OrderTimeSpanEnum span)
    {
      switch (span)
      {
        case OrderTimeSpanEnum.Day: return "day";
        case OrderTimeSpanEnum.Fok: return "fok";
        case OrderTimeSpanEnum.Gtc: return "gtc";
        case OrderTimeSpanEnum.Ioc: return "ioc";
        case OrderTimeSpanEnum.Am: return "opg";
        case OrderTimeSpanEnum.Pm: return "cls";
      }

      return null;
    }

    /// <summary>
    /// Convert child orders to brackets
    /// </summary>
    /// <param name="order"></param>
    /// <param name="direction"></param>
    /// <returns></returns>
    protected OrderAdvancedAttributesMessage GetExternalBracket(OrderModel order, double direction)
    {
      var nextOrder = order
        .Orders
        .FirstOrDefault(o => (o.Price - order.Price) * direction > 0);

      if (nextOrder is not null)
      {
        return new OrderAdvancedAttributesMessage { StopPrice = nextOrder.Price };
      }

      return null;
    }

    /// <summary>
    /// Convert remote order to local
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    protected OrderModel GetInternalOrder(OrderMessage order)
    {
      var instrument = new Instrument
      {
        Name = order.Symbol
      };

      var action = new TransactionModel
      {
        Id = $"{order.OrderId}",
        Descriptor = order.ClientOrderId,
        Instrument = instrument,
        CurrentVolume = order.FilledQuantity,
        Volume = order.Quantity,
        Time = order.CreatedAtUtc,
        Status = GetInternalStatus(order.OrderStatus)
      };

      var inOrder = new OrderModel
      {
        Transaction = action,
        Type = OrderTypeEnum.Market,
        Side = GetInternalOrderSide(order.OrderSide),
        TimeSpan = GetInternalTimeSpan(order.TimeInForce)
      };

      switch (order.OrderType)
      {
        case "stop":
          inOrder.Type = OrderTypeEnum.Stop;
          inOrder.Price = order.StopPrice;
          break;

        case "limit":
          inOrder.Type = OrderTypeEnum.Limit;
          inOrder.Price = order.LimitPrice;
          break;

        case "stop_limit":
          inOrder.Type = OrderTypeEnum.StopLimit;
          inOrder.Price = order.StopPrice;
          inOrder.ActivationPrice = order.LimitPrice;
          break;
      }

      return inOrder;
    }

    /// <summary>
    /// Convert remote position to local
    /// </summary>
    /// <param name="position"></param>
    /// <returns></returns>
    protected PositionModel GetInternalPosition(PositionMessage position)
    {
      var instrument = new Instrument
      {
        Name = position.Symbol
      };

      var action = new TransactionModel
      {
        Id = $"{position.AssetId}",
        Descriptor = position.Symbol,
        Instrument = instrument,
        Price = position.AverageEntryPrice,
        CurrentVolume = position.AvailableQuantity,
        Volume = position.Quantity
      };

      var order = new OrderModel
      {
        Transaction = action,
        Type = OrderTypeEnum.Market,
        Side = GetInternalPositionSide(position.Side)
      };

      var gainLossPoints = position.AverageEntryPrice - position.AssetCurrentPrice;
      var gainLoss = position.CostBasis - position.MarketValue;

      return new PositionModel
      {
        GainLossPointsMax = gainLossPoints,
        GainLossPointsMin = gainLossPoints,
        GainLossPoints = gainLossPoints,
        GainLossMax = gainLoss,
        GainLossMin = gainLoss,
        GainLoss = gainLoss,
        Order = order,
        Orders = [order]
      };
    }

    /// <summary>
    /// Convert remote order status to local
    /// </summary>
    /// <param name="status"></param>
    /// <returns></returns>
    protected OrderStatusEnum GetInternalStatus(string status)
    {
      switch (status)
      {
        case "fill":
        case "filled": return OrderStatusEnum.Filled;
        case "partial_fill":
        case "partially_filled": return OrderStatusEnum.Partitioned;
        case "stopped":
        case "expired":
        case "rejected":
        case "canceled":
        case "done_for_day": return OrderStatusEnum.Canceled;
        case "new":
        case "held":
        case "accepted":
        case "suspended":
        case "pending_new":
        case "pending_cancel":
        case "pending_replace": return OrderStatusEnum.Pending;
      }

      return OrderStatusEnum.None;
    }

    /// <summary>
    /// Convert remote order side to local
    /// </summary>
    /// <param name="side"></param>
    /// <returns></returns>
    protected OrderSideEnum GetInternalOrderSide(string side)
    {
      switch (side)
      {
        case "buy": return OrderSideEnum.Buy;
        case "sell": return OrderSideEnum.Sell;
      }

      return OrderSideEnum.None;
    }

    /// <summary>
    /// Convert remote position side to local
    /// </summary>
    /// <param name="side"></param>
    /// <returns></returns>
    protected OrderSideEnum GetInternalPositionSide(string side)
    {
      switch (side)
      {
        case "long": return OrderSideEnum.Buy;
        case "short": return OrderSideEnum.Sell;
      }

      return OrderSideEnum.None;
    }

    /// <summary>
    /// Convert remote time in force to local
    /// </summary>
    /// <param name="span"></param>
    /// <returns></returns>
    protected OrderTimeSpanEnum GetInternalTimeSpan(string span)
    {
      switch (span)
      {
        case "day": return OrderTimeSpanEnum.Day;
        case "fok": return OrderTimeSpanEnum.Fok;
        case "gtc": return OrderTimeSpanEnum.Gtc;
        case "ioc": return OrderTimeSpanEnum.Ioc;
        case "opg": return OrderTimeSpanEnum.Am;
        case "cls": return OrderTimeSpanEnum.Pm;
      }

      return OrderTimeSpanEnum.None;
    }
  }
}
