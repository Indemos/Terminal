using Alpaca.Mappers;
using Alpaca.Messages;
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
using Terminal.Core.Extensions;
using Terminal.Core.Models;

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
      var errors = new List<ErrorModel>();

      try
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
      }
      catch (Exception e)
      {
        errors.Add(new ErrorModel { ErrorMessage = $"{e}" });
      }

      return errors;
    }

    /// <summary>
    /// Subscribe to data streams
    /// </summary>
    public override async Task<IList<ErrorModel>> Subscribe(string name)
    {
      var errors = new List<ErrorModel>();

      try
      {
        await Unsubscribe(name);
        await SendStream(_streamer, new SubscriptionUpdateMessage
        {
          Action = "subscribe",
          Trades = [name],
          Quotes = [name]
        });
      }
      catch (Exception e)
      {
        errors.Add(new ErrorModel { ErrorMessage = $"{e}" });
      }

      return errors;
    }

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public override Task<IList<ErrorModel>> Disconnect()
    {
      _connections?.ForEach(o => o?.Dispose());
      _connections?.Clear();

      return Task.FromResult<IList<ErrorModel>>([]);
    }

    /// <summary>
    /// Unsubscribe from data streams
    /// </summary>
    public override async Task<IList<ErrorModel>> Unsubscribe(string name)
    {
      var errors = new List<ErrorModel>();

      try
      {
        await SendStream(_streamer, new SubscriptionUpdateMessage
        {
          Action = "unsubscribe",
          Trades = [name],
          Quotes = [name]
        });

        _subscriptions?.ForEach(o => o.Dispose());
        _subscriptions?.Clear();
      }
      catch (Exception e)
      {
        errors.Add(new ErrorModel { ErrorMessage = $"{e}" });
      }

      return errors;
    }

    /// <summary>
    /// Get options
    /// </summary>
    /// <param name="message"></param>
    /// <param name="props"></param>
    /// <returns></returns>
    public override async Task<ResponseItemModel<IList<OptionModel>>> GetOptions(OptionMessageModel message, Hashtable props = null)
    {
      var response = new ResponseItemModel<IList<OptionModel>>();

      try
      {
        var criteria = new Hashtable
        {
          ["limit"] = 1000,
          ["underlying_symbol"] = message.Name,
          ["expiration_date_gte"] = message.MinDate,
          ["expiration_date_lte"] = message.MaxDate
        };

        foreach (DictionaryEntry prop in props ?? [])
        {
          criteria[prop.Key] = prop.Value;
        }

        var optionResponse = await SendData<LatestDataMessage<
          HistoricalQuoteMessage,
          HistoricalBarMessage,
          HistoricalTradeMessage,
          OptionSnapshotMessage,
          HistoricalOrderBookMessage>>
        ($"/v1beta1/options/snapshots/{message.Name}?{criteria.ToQuery()}");

        response.Data = optionResponse
          .Data
          .Snapshots
          .Select(option => InternalMap.GetOption(option.Value))
          .ToList();
      }
      catch (Exception e)
      {
        response.Errors = [new ErrorModel { ErrorMessage = $"{e}" }];
      }

      return response;
    }

    /// <summary>
    /// Get latest quote
    /// </summary>
    /// <param name="message"></param>
    /// <param name="props"></param>
    /// <returns></returns>
    public override async Task<ResponseItemModel<IDictionary<string, PointModel>>> GetPoint(PointMessageModel message, Hashtable props = null)
    {
      var response = new ResponseItemModel<IDictionary<string, PointModel>>();

      try
      {
        var criteria = new Hashtable
        {
          ["symbols"] = string.Join(",", message.Names),
          ["feed"] = "iex"
        };

        foreach (DictionaryEntry prop in props ?? [])
        {
          criteria[prop.Key] = prop.Value;
        }

        var pointResponse = await SendData<LatestDataMessage<
          HistoricalQuoteMessage,
          HistoricalBarMessage,
          HistoricalTradeMessage,
          SnapshotMessage,
          HistoricalOrderBookMessage>
        >($"/v2/stocks/quotes/latest?{criteria.ToQuery()}");

        response.Data = message
          .Names
          .Select(name => InternalMap.GetPoint(pointResponse.Data.Quotes[name]))
          .ToDictionary(o => o.Instrument.Name);
      }
      catch (Exception e)
      {
        response.Errors = [new ErrorModel { ErrorMessage = $"{e}" }];
      }

      return response;
    }

    /// <summary>
    /// Get historical bars
    /// </summary>
    /// <param name="message"></param>
    /// <param name="props"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public override async Task<ResponseItemModel<IList<PointModel>>> GetPoints(PointMessageModel message, Hashtable props = null)
    {
      var response = new ResponseItemModel<IList<PointModel>>();

      try
      {
        var names = string.Join(",", message.Names);
        var criteria = new Hashtable
        {
          ["limit"] = 1000,
          ["symbol"] = names,
          ["start"] = message.MinDate,
          ["end"] = message.MaxDate
        };

        foreach (DictionaryEntry prop in props ?? [])
        {
          criteria[prop.Key] = prop.Value;
        }

        var pointResponse = await SendData<LatestDataMessage<
          HistoricalQuoteMessage,
          HistoricalBarMessage,
          HistoricalTradeMessage,
          OptionSnapshotMessage,
          HistoricalOrderBookMessage>>
          ($"/v2/stocks/{names}/bars?{criteria.ToQuery()}");

        response.Data = pointResponse
          .Data
          .Bars
          .Select(o => InternalMap.GetBar(o.Value))
          .ToList();
      }
      catch (Exception e)
      {
        response.Errors = [new ErrorModel { ErrorMessage = $"{e}" }];
      }

      return response;
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
    protected virtual async Task GetAccountData()
    {
      var account = await SendData<AccountMessage>("/v2/account");
      var positions = await SendData<PositionMessage[]>("/v2/positions");
      var orders = await SendData<OrderMessage[]>("/v2/orders");

      Account.Balance = account.Data.Equity;
      Account.Descriptor = account.Data.AccountNumber;
      Account.ActiveOrders = orders.Data.Select(InternalMap.GetOrder).ToDictionary(o => o.Transaction.Id, o => o);
      Account.ActivePositions = positions.Data.Select(InternalMap.GetPosition).ToDictionary(o => o.Order.Transaction.Id, o => o);

      Account.ActiveOrders.ForEach(async o => await Subscribe(o.Value.Transaction.Instrument.Name));
      Account.ActivePositions.ForEach(async o => await Subscribe(o.Value.Order.Transaction.Instrument.Name));
    }

    /// <summary>
    /// Send data to web socket stream
    /// </summary>
    /// <param name="ws"></param>
    /// <param name="data"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    protected virtual Task SendStream(ClientWebSocket ws, object data, CancellationTokenSource cancellation = null)
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
    protected virtual async Task<ClientWebSocket> GetConnection(ClientWebSocket ws, ScheduleService scheduler)
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
    protected virtual void ProcessPoint(string content)
    {
      var streamPoint = JsonSerializer
        .Deserialize<HistoricalQuoteMessage[]>(content, _sender.Options)
        .FirstOrDefault();

      var instrument = Account.Instruments.Get(streamPoint.Symbol) ?? new Instrument();
      var point = new PointModel
      {
        Ask = streamPoint.AskPrice,
        Bid = streamPoint.BidPrice,
        AskSize = streamPoint.AskSize ?? 0,
        BidSize = streamPoint.BidSize ?? 0,
        Last = streamPoint.BidPrice ?? streamPoint.AskPrice,
        Time = streamPoint.TimestampUtc ?? DateTime.Now,
        TimeFrame = instrument.TimeFrame,
        Instrument = instrument
      };

      instrument.Name = streamPoint.Symbol;
      instrument.Points.Add(point);
      instrument.PointGroups.Add(point);
    }

    /// <summary>
    /// Process quote from the stream
    /// </summary>
    /// <param name="content"></param>
    protected virtual void ProcessTrade(string content)
    {
      var orderMessage = JsonSerializer
        .Deserialize<HistoricalTradeMessage[]>(content, _sender.Options)
        .FirstOrDefault();

      var action = new TransactionModel
      {
        Id = orderMessage.TradeId,
        Time = orderMessage.TimestampUtc,
        Price = orderMessage.Price,
        Volume = orderMessage.Size,
        Instrument = new Instrument { Name = orderMessage.Symbol }
      };

      var order = new OrderModel
      {
        Side = InternalMap.GetOrderSide(orderMessage.TakerSide)
      };

      var message = new StateModel<OrderModel>
      {
        Next = order
      };

      OrderStream(message);
    }

    /// <summary>
    /// Send data to the API
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="verb"></param>
    /// <param name="content"></param>
    /// <returns></returns>
    protected virtual async Task<Distribution.Stream.Models.ResponseModel<T>> SendData<T>(
      string source,
      HttpMethod verb = null,
      object content = null)
    {
      var uri = new UriBuilder(DataUri + source);
      var message = new HttpRequestMessage
      {
        Method = verb ?? HttpMethod.Get
      };

      switch (true)
      {
        case true when Equals(message.Method, HttpMethod.Put):
        case true when Equals(message.Method, HttpMethod.Post):
        case true when Equals(message.Method, HttpMethod.Patch): message.Content = new StringContent(JsonSerializer.Serialize(content)); break;
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
    protected virtual async Task<ResponseItemModel<OrderModel>> CreateOrder(OrderModel order)
    {
      var inResponse = new ResponseItemModel<OrderModel>();

      try
      {
        var exOrder = ExternalMap.GetOrder(order);
        var exResponse = await SendData<OrderMessage>("/v2/orders", HttpMethod.Post, exOrder);

        inResponse.Data = order;
        inResponse.Data.Transaction.Id = $"{exResponse.Data.OrderId}";
        inResponse.Data.Transaction.Status = InternalMap.GetStatus(exResponse.Data.OrderStatus);

        if ((int)exResponse.Message.StatusCode < 400)
        {
          Account.ActiveOrders.Add(order.Transaction.Id, order);
        }
      }
      catch (Exception e)
      {
        inResponse.Errors.Add(new ErrorModel { ErrorMessage = $"{e}" });
      }

      return inResponse;
    }

    /// <summary>
    /// Cancel order
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    protected virtual async Task<ResponseItemModel<OrderModel>> DeleteOrder(OrderModel order)
    {
      var inResponse = new ResponseItemModel<OrderModel>();

      try
      {
        var exResponse = await SendData<OrderMessage>($"/v2/orders/{order.Transaction.Id}", HttpMethod.Delete);

        inResponse.Data = order;
        inResponse.Data.Transaction.Id = $"{exResponse.Data.OrderId}";
        inResponse.Data.Transaction.Status = InternalMap.GetStatus(exResponse.Data.OrderStatus);

        if ((int)exResponse.Message.StatusCode < 400)
        {
          Account.ActiveOrders.Remove(order.Transaction.Id);
        }
      }
      catch (Exception e)
      {
        inResponse.Errors.Add(new ErrorModel { ErrorMessage = $"{e}" });
      }

      return inResponse;
    }
  }
}
