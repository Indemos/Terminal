using Distribution.Services;
using Distribution.Stream;
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
using Tradier.Messages.Account;
using Tradier.Messages.Stream;
using Tradier.Messages.Trading;
using Dis = Distribution.Stream.Models;

namespace Tradier
{
  public partial class Adapter : Gateway
  {
    /// <summary>
    /// HTTP client
    /// </summary>
    protected Service sender;

    /// <summary>
    /// Event session
    /// </summary>
    protected string dataSession;

    /// <summary>
    /// Account session
    /// </summary>
    protected string accountSession;

    /// <summary>
    /// Web socket for events
    /// </summary>
    protected ClientWebSocket dataStreamer;

    /// <summary>
    /// Web socket for account
    /// </summary>
    protected ClientWebSocket accountStreamer;

    /// <summary>
    /// Disposable connections
    /// </summary>
    protected IList<IDisposable> connections;

    /// <summary>
    /// API key
    /// </summary>
    public string Token { get; set; }

    /// <summary>
    /// API key for streaming
    /// </summary>
    public string SessionToken { get; set; }

    /// <summary>
    /// HTTP endpoint
    /// </summary>
    public string DataUri { get; set; }

    /// <summary>
    /// Socket endpoint
    /// </summary>
    public string StreamUri { get; set; }

    /// <summary>
    /// Streaming authentication endpoint
    /// </summary>
    public string SessionUri { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public Adapter()
    {
      connections = [];

      DataUri = "https://sandbox.tradier.com/v1";
      SessionUri = "https://api.tradier.com/v1";
      StreamUri = "wss://ws.tradier.com/v1";
    }

    /// <summary>
    /// Connect
    /// </summary>
    /// <returns></returns>
    public override async Task<ResponseModel<StatusEnum>> Connect()
    {
      var response = new ResponseModel<StatusEnum>();

      try
      {
        var sender = new Service() { Timeout = TimeSpan.FromDays(1) };
        var scheduler = new ScheduleService();
        var dataStreamer = new ClientWebSocket();
        var accountStreamer = new ClientWebSocket();

        await Disconnect();

        this.sender = sender;
        this.dataStreamer = dataStreamer;
        this.accountStreamer = accountStreamer;
        this.dataSession = (await GetMarketSession())?.Stream?.Session;
        this.accountSession = (await GetAccountSession()).Stream?.Session;

        await GetAccount([]);
        await GetConnection("/markets/events", dataStreamer, scheduler, message =>
        {
          var messageType = $"{message["type"]}";

          switch (messageType)
          {
            case "quote":

              var quoteMessage = message.Deserialize<QuoteMessage>(sender.Options);
              var point = GetPrice(quoteMessage);
              var summary = Account.State[quoteMessage.Symbol];

              point.Instrument = summary.Instrument;
              summary.Points.Add(point);
              summary.PointGroups.Add(point, summary.Instrument.TimeFrame);
              summary.Instrument.Point = summary.PointGroups.Last();

              DataStream(new MessageModel<PointModel> { Next = summary.Instrument.Point });

              break;

            case "trade": break;
            case "tradex": break;
            case "summary": break;
            case "timesale": break;
          }
        });

        await GetConnection("/accounts/events", accountStreamer, scheduler, message =>
        {
          var order = GetStreamOrder(message.Deserialize<OrderMessage>(this.sender.Options));
          var container = new MessageModel<OrderModel> { Next = order };

          OrderStream(container);
        });

        connections.Add(sender);
        connections.Add(scheduler);
        connections.Add(dataStreamer);
        connections.Add(accountStreamer);

        await Task.WhenAll(Account.State.Values.Select(o => Subscribe(o.Instrument)));

        response.Data = StatusEnum.Active;
      }
      catch (Exception e)
      {
        response.Errors.Add(new ErrorModel { ErrorMessage = $"{e}" });
      }

      return response;
    }

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public override Task<ResponseModel<StatusEnum>> Disconnect()
    {
      var response = new ResponseModel<StatusEnum>();

      try
      {
        connections?.ForEach(o => o?.Dispose());
        connections?.Clear();

        response.Data = StatusEnum.Active;
      }
      catch (Exception e)
      {
        response.Errors.Add(new ErrorModel { ErrorMessage = $"{e}" });
      }

      return Task.FromResult(response);
    }

    /// <summary>
    /// Subscribe to data streams
    /// </summary>
    /// <param name="instrument"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<StatusEnum>> Subscribe(InstrumentModel instrument)
    {
      var response = new ResponseModel<StatusEnum>
      {
        Data = StatusEnum.Active
      };

      try
      {
        await Unsubscribe(instrument);

        Account.State[instrument.Name] = Account.State.Get(instrument.Name) ?? new StateModel();
        Account.State[instrument.Name].Instrument ??= instrument;

        var names = Account
          .State
          .Values
          .Select(o => o.Instrument.Name)
          .Distinct();

        var dataMessage = new DataMessage
        {
          Symbols = [.. names],
          Filter = ["trade", "quote", "summary", "timesale", "tradex"],
          Session = dataSession
        };

        var accountMessage = new Messages.Stream.AccountMessage
        {
          Events = ["order"],
          Session = accountSession
        };

        await SendStream(dataStreamer, dataMessage);
        await SendStream(accountStreamer, accountMessage);

      }
      catch (Exception e)
      {
        response.Errors.Add(new ErrorModel { ErrorMessage = $"{e}" });
      }

      return response;
    }

    /// <summary>
    /// Unsubscribe from data streams
    /// </summary>
    /// <param name="instrument"></param>
    /// <returns></returns>
    public override Task<ResponseModel<StatusEnum>> Unsubscribe(InstrumentModel instrument)
    {
      var response = new ResponseModel<StatusEnum>
      {
        Data = StatusEnum.Active
      };

      return Task.FromResult(response);
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
        var num = Account.Descriptor;
        var account = await GetBalances(num);
        var orders = await GetOrders(null, criteria);
        var positions = await GetPositions(null, criteria);
        var openOrders = orders.Data.Where(o => o.Transaction.Status is OrderStatusEnum.Pending or OrderStatusEnum.Partitioned);

        Account.Balance = account.TotalEquity;
        Account.Orders = openOrders.GroupBy(o => o.Id).ToDictionary(o => o.Key, o => o.FirstOrDefault()).Concurrent();
        Account.Positions = positions.Data.GroupBy(o => o.Name).ToDictionary(o => o.Key, o => o.FirstOrDefault()).Concurrent();

        Account
          .Positions
          .Values
          .ForEach(o =>
          {
            Account.State[o.Name] = Account.State.Get(o.Name) ?? new StateModel();
            Account.State[o.Name].Instrument = o.Transaction.Instrument;
          });

        response.Data = Account;
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
    /// <param name="screener"></param>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<DomModel>> GetDom(PointScreenerModel screener, Hashtable criteria)
    {
      var response = new ResponseModel<DomModel>();

      try
      {
        var name = screener.Instrument.Name;
        var pointResponse = await GetQuotes([name], true);
        var point = GetPrice(pointResponse?.Items?.FirstOrDefault());

        response.Data = new DomModel
        {
          Asks = [point],
          Bids = [point]
        };
      }
      catch (Exception e)
      {
        response.Errors = [new ErrorModel { ErrorMessage = $"{e}" }];
      }

      return response;
    }

    /// <summary>
    /// Get historical ticks
    /// </summary>
    /// <param name="screener"></param>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override Task<ResponseModel<IList<PointModel>>> GetPoints(PointScreenerModel screener, Hashtable criteria)
    {
      throw new NotImplementedException();
    }

    /// <summary>
    /// Get options
    /// </summary>
    /// <param name="screener"></param>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<IList<InstrumentModel>>> GetOptions(InstrumentScreenerModel screener, Hashtable criteria)
    {
      var response = new ResponseModel<IList<InstrumentModel>>();

      try
      {
        var optionResponse = await GetOptionChain(screener.Instrument.Name, screener.MaxDate ?? screener.MinDate);

        response.Data = optionResponse
          .Options
          ?.Select(GetOption)
          ?.OrderBy(o => o.Derivative.ExpirationDate)
          ?.ThenBy(o => o.Derivative.Strike)
          ?.ThenBy(o => o.Derivative.Side)
          ?.ToList() ?? [];
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
    /// <param name="screener"></param>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<IList<OrderModel>>> GetPositions(PositionScreenerModel screener, Hashtable criteria)
    {
      var response = new ResponseModel<IList<OrderModel>>();

      try
      {
        var exPositions = await GetPositions(Account.Descriptor);
        var positions = exPositions?.Select(GetPosition)?.ToList();

        response.Data = positions ?? [];
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
    /// <param name="screener"></param>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<IList<OrderModel>>> GetOrders(OrderScreenerModel screener, Hashtable criteria)
    {
      var response = new ResponseModel<IList<OrderModel>>();

      try
      {
        response.Data = (await GetOrders(Account.Descriptor))?.SelectMany(GetOrders)?.ToList() ?? [];
      }
      catch (Exception e)
      {
        response.Errors = [new ErrorModel { ErrorMessage = $"{e}" }];
      }

      return response;
    }

    /// <summary>
    /// Create orders
    /// </summary>
    /// <param name="orders"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<IList<OrderModel>>> SendOrders(params OrderModel[] orders)
    {
      var response = new ResponseModel<IList<OrderModel>> { Data = [] };

      foreach (var order in orders)
      {
        try
        {
          response.Data.Add(await SendOrder(order));
        }
        catch (Exception e)
        {
          response.Errors.Add(new ErrorModel { ErrorMessage = $"{e}" });
        }
      }

      await GetAccount([]);

      return response;
    }

    /// <summary>
    /// Cancel orders
    /// </summary>
    /// <param name="orders"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<IList<OrderModel>>> ClearOrders(params OrderModel[] orders)
    {
      var response = new ResponseModel<IList<OrderModel>> { Data = [] };

      foreach (var order in orders)
      {
        try
        {
          if (Equals((await ClearOrder(order.Transaction.Id))?.Status?.ToUpper(), "OK"))
          {
            response.Data.Add(order);
          }
        }
        catch (Exception e)
        {
          response.Errors.Add(new ErrorModel { ErrorMessage = $"{e}" });
        }
      }

      await GetAccount([]);

      return response;
    }

    /// <summary>
    /// Send data to the API
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="verb"></param>
    /// <param name="content"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public virtual async Task<Dis.ResponseModel<T>> Send<T>(string source, HttpMethod verb = null, object content = null, string token = null)
    {
      var uri = new UriBuilder(source);
      var message = new HttpRequestMessage { Method = verb ?? HttpMethod.Get };

      switch (true)
      {
        case true when Equals(message.Method, HttpMethod.Put):
        case true when Equals(message.Method, HttpMethod.Post):
        case true when Equals(message.Method, HttpMethod.Patch):
          message.Content = new StringContent(JsonSerializer.Serialize(content, sender.Options), Encoding.UTF8, "application/json");
          break;
      }

      message.RequestUri = uri.Uri;
      message.Headers.Add("Accept", "application/json");
      message.Headers.Add("Authorization", $"Bearer {token ?? Token}");

      var response = await sender.Send<T>(message, sender.Options);

      if (response.Message.IsSuccessStatusCode is false)
      {
        throw new HttpRequestException(await response.Message.Content.ReadAsStringAsync(), null, response.Message.StatusCode);
      }

      return response;
    }

    /// <summary>
    /// Create order
    /// </summary>
    /// <param name="order"></param>
    /// <param name="preview"></param>
    /// <returns></returns>
    protected virtual async Task<OrderModel> SendOrder(OrderModel order, bool preview = false)
    {
      var response = null as OrderResponseMessage;

      Account.Orders[order.Id] = order;

      await Task.WhenAll(order
        .Orders
        .Append(order)
        .Where(o => o.Transaction?.Instrument is not null)
        .Select(o => Subscribe(o.Transaction.Instrument)));

      if (order.Orders.IsEmpty())
      {
        switch (order.Transaction.Instrument.Type)
        {
          case InstrumentEnum.Shares: response = await SendEquityOrder(order, preview); break;
          case InstrumentEnum.Options: response = await SendOptionOrder(order, preview); break;
        }
      }
      else
      {
        var isBrace = order.Orders.Any(o => o.Instruction is InstructionEnum.Brace);
        var isCombo = order.Orders.Append(order).Any(o => o?.Transaction?.Instrument?.Type is InstrumentEnum.Shares);

        switch (true)
        {
          case true when isBrace: response = await SendBraceOrder(order, preview); break;
          case true when isCombo: response = await SendComboOrder(order, preview); break;
          case true when isCombo is false: response = await SendGroupOrder(order, preview); break;
        }
      }

      order.Transaction.Id = $"{response?.Id}";
      order.Transaction.Status = Equals(response?.Status?.ToUpper(), "OK") ? OrderStatusEnum.Filled : order.Transaction.Status;

      return order;
    }

    /// <summary>
    /// Send data to web socket stream
    /// </summary>
    /// <param name="streamer"></param>
    /// <param name="data"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    protected virtual Task SendStream(ClientWebSocket streamer, object data, CancellationTokenSource cancellation = null)
    {
      var content = JsonSerializer.Serialize(data, sender.Options);
      var message = Encoding.UTF8.GetBytes(content);

      return streamer.SendAsync(
        message,
        WebSocketMessageType.Text,
        true,
        cancellation?.Token ?? CancellationToken.None);
    }

    /// <summary>
    /// Web socket stream
    /// </summary>
    /// <param name="uri"></param>
    /// <param name="streamer"></param>
    /// <param name="scheduler"></param>
    /// <param name="action"></param>
    /// <returns></returns>
    protected virtual async Task GetConnection(string uri, ClientWebSocket streamer, ScheduleService scheduler, Action<JsonNode> action)
    {
      var source = new UriBuilder($"{StreamUri}{uri}");
      var cancellation = new CancellationTokenSource();

      await streamer.ConnectAsync(source.Uri, cancellation.Token);

      scheduler.Send(async () =>
      {
        while (streamer.State is WebSocketState.Open)
        {
          try
          {
            var data = new byte[short.MaxValue];
            var streamResponse = await streamer.ReceiveAsync(data, cancellation.Token);
            var content = $"{Encoding.Default.GetString(data).Trim(['\0', '[', ']'])}";

            action(JsonNode.Parse(content));
          }
          catch (Exception e)
          {
            InstanceService<MessageService>.Instance.OnMessage(new MessageModel<string> { Error = e });
          }
        }
      });
    }
  }
}
