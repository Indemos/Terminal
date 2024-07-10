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

        await GetAccount([]);

        _connections.Add(_sender);
        _connections.Add(_streamer);
        _connections.Add(scheduler);

        Account.Instruments.ForEach(async o => await Subscribe(o.Value));
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
    /// <param name="instrument"></param>
    /// <returns></returns>
    public override async Task<IList<ErrorModel>> Subscribe(InstrumentModel instrument)
    {
      var errors = new List<ErrorModel>();

      try
      {
        await Unsubscribe(instrument);
        await SendStream(_streamer, new SubscriptionUpdateMessage
        {
          Action = "subscribe",
          Trades = [instrument.Name],
          Quotes = [instrument.Name]
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
      var errors = new List<ErrorModel>();

      try
      {
        _connections?.ForEach(o => o?.Dispose());
        _connections?.Clear();
      }
      catch (Exception e)
      {
        errors.Add(new ErrorModel { ErrorMessage = $"{e}" });
      }

      return Task.FromResult<IList<ErrorModel>>(errors);
    }

    /// <summary>
    /// Unsubscribe from data streams
    /// </summary>
    /// <param name="instrument"></param>
    /// <returns></returns>
    public override async Task<IList<ErrorModel>> Unsubscribe(InstrumentModel instrument)
    {
      var errors = new List<ErrorModel>();

      try
      {
        await SendStream(_streamer, new SubscriptionUpdateMessage
        {
          Action = "unsubscribe",
          Trades = [instrument.Name],
          Quotes = [instrument.Name]
        });
      }
      catch (Exception e)
      {
        errors.Add(new ErrorModel { ErrorMessage = $"{e}" });
      }

      return errors;
    }

    /// <summary>
    /// Sync open balance, order, and positions 
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<IAccount>> GetAccount(Hashtable criteria)
    {
      var response = new ResponseModel<IAccount>();

      try
      {
        var account = await SendData<AccountMessage>("/v2/account");
        var positions = await GetPositions(null, criteria);
        var orders = await GetOrders(null, criteria);

        Account.Balance = account.Data.Equity;
        Account.ActiveOrders = orders.Data.ToDictionary(o => o.Transaction.Id);
        Account.ActivePositions = positions.Data.ToDictionary(o => o.Order.Transaction.Id);

        Account
          .ActiveOrders
          .Select(o => o.Value.Transaction.Instrument.Name)
          .Concat(Account.ActivePositions.Select(o => o.Value.Order.Transaction.Instrument.Name))
          .Where(o => Account.Instruments.ContainsKey(o) is false)
          .ForEach(o => Account.Instruments.Add(o, new InstrumentModel { Name = o }));

        response.Data = Account;
      }
      catch (Exception e)
      {
        response.Errors = [new ErrorModel { ErrorMessage = $"{e}" }];
      }

      return response;
    }

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="args"></param>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<IList<OrderModel>>> GetOrders(OrdersArgs args, Hashtable criteria)
    {
      var response = new ResponseModel<IList<OrderModel>>();

      try
      {
        var items = await SendData<OrderMessage[]>($"/v2/orders?{criteria.ToQuery()}");

        response.Data = [.. items.Data.Select(InternalMap.GetOrder)];
      }
      catch (Exception e)
      {
        response.Errors = [new ErrorModel { ErrorMessage = $"{e}" }];
      }

      return response;
    }

    /// <summary>
    /// Get positions 
    /// </summary>
    /// <param name="args"></param>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<IList<PositionModel>>> GetPositions(PositionsArgs args, Hashtable criteria)
    {
      var response = new ResponseModel<IList<PositionModel>>();

      try
      {
        var items = await SendData<PositionMessage[]>($"/v2/positions?{criteria.ToQuery()}");

        response.Data = [.. items.Data.Select(InternalMap.GetPosition)];
      }
      catch (Exception e)
      {
        response.Errors = [new ErrorModel { ErrorMessage = $"{e}" }];
      }

      return response;
    }

    /// <summary>
    /// Get options
    /// </summary>
    /// <param name="args"></param>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<IList<OptionModel>>> GetOptions(OptionsArgs args, Hashtable criteria)
    {
      var response = new ResponseModel<IList<OptionModel>>();

      try
      {
        var props = new Hashtable
        {
          ["underlying_symbol"] = args.Name,
          ["expiration_date_gte"] = args.MinDate,
          ["expiration_date_lte"] = args.MaxDate

        }.Merge(criteria);

        var optionResponse = await SendData<DataMessage<OptionSnapshotMessage>>($"/v1beta1/options/snapshots/{props["underlying_symbol"]}?{props}");

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
    /// <param name="args"></param>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<DomModel>> GetDom(InstrumentArgs args, Hashtable criteria)
    {
      var response = new ResponseModel<DomModel>();

      try
      {
        var props = new Hashtable
        {
          ["loc"] = "us",
          ["symbols"] = args.Name,
          ["security"] = args.Security

        }.Merge(criteria);

        var source = string.Empty;

        switch (props["security"])
        {
          case "STK": source = $"/v2/stocks/quotes/latest?{props}"; break;
          case "CRYPTO": source = $"/v1beta3/crypto/{props["loc"]}/latest/quotes?{props}"; break;
        }

        var pointResponse = await SendData<DataMessage<SnapshotMessage>>(source);

        response.Data = InternalMap.GetDom(pointResponse.Data.Quotes[props["symbols"]]);
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
    /// <param name="args"></param>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<IList<PointModel>>> GetPoints(PointsArgs args, Hashtable criteria)
    {
      var response = new ResponseModel<IList<PointModel>>();

      try
      {
        var props = new Hashtable
        {
          ["loc"] = "us",
          ["symbols"] = args.Name,
          ["security"] = args.Security

        }.Merge(criteria);

        var source = string.Empty;

        switch (props["security"])
        {
          case "STK": source = $"/v2/stocks/quotes?{props}"; break;
          case "CRYPTO": source = $"/v1beta3/crypto/{props["loc"]}/quotes?{props}"; break;
        }

        var pointResponse = await SendData<DataMessage<SnapshotMessage>>(source);

        response.Data = pointResponse
          .Data
          .Quotes
          .Select(o => InternalMap.GetPoint(o.Value))
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
        .Deserialize<QuoteMessage[]>(content, _sender.Options)
        .FirstOrDefault();

      var instrument = Account.Instruments.Get(streamPoint.Symbol) ?? new InstrumentModel();
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
        .Deserialize<TradeMessage[]>(content, _sender.Options)
        .FirstOrDefault();

      var action = new TransactionModel
      {
        Id = orderMessage.TradeId,
        Time = orderMessage.TimestampUtc,
        Price = orderMessage.Price,
        Volume = orderMessage.Size,
        Instrument = new InstrumentModel { Name = orderMessage.Symbol }
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
    protected virtual async Task<ResponseModel<OrderModel>> CreateOrder(OrderModel order)
    {
      var inResponse = new ResponseModel<OrderModel>();

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
    protected virtual async Task<ResponseModel<OrderModel>> DeleteOrder(OrderModel order)
    {
      var inResponse = new ResponseModel<OrderModel>();

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
