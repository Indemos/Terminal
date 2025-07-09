using Distribution.Services;
using Distribution.Stream;
using Flurl.Http;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
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
using Tradier.Messages.Account;
using Tradier.Messages.Stream;
using Tradier.Messages.Trading;
using static System.Net.Mime.MediaTypeNames;

namespace Tradier
{
  public partial class Adapter : Gateway
  {
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
      return await Response(async () =>
      {
        var scheduler = new ScheduleService();
        var dataStreamer = new ClientWebSocket();
        var accountStreamer = new ClientWebSocket();
        var sender = InstanceService<Service>.Instance;

        await Disconnect();

        this.dataStreamer = dataStreamer;
        this.accountStreamer = accountStreamer;
        this.dataSession = (await GetMarketSession())?.Stream?.Session;
        this.accountSession = (await GetAccountSession()).Stream?.Session;

        await GetAccount();
        await GetConnection("/markets/events", dataStreamer, scheduler, message =>
        {
          var messageType = $"{message["type"]}";

          switch (messageType)
          {
            case "quote":

              var quoteMessage = message.Deserialize<QuoteMessage>(sender.Options);
              var summary = Account.States.Get(quoteMessage.Symbol);
              var point = Downstream.GetPrice(quoteMessage);

              point.Bar = null;
              point.Account = Account;
              point.Name = quoteMessage.Symbol;
              point.TimeFrame = summary.TimeFrame;

              summary.Instrument.Point = point;
              summary.Points.Add(point);
              summary.PointGroups.Add(point, summary.TimeFrame);
              summary.Instrument.Point = summary.PointGroups.Last();

              Stream(new MessageModel<PointModel> { Next = summary.Instrument.Point });

              break;

            case "trade": break;
            case "tradex": break;
            case "summary": break;
            case "timesale": break;
          }
        });

        await GetConnection("/accounts/events", accountStreamer, scheduler, message =>
        {
          var order = Downstream.GetStreamOrder(message.Deserialize<OrderMessage>(sender.Options));
          var container = new MessageModel<OrderModel> { Next = order };

          OrderStream(container);
        });

        connections.Add(dataStreamer);
        connections.Add(accountStreamer);

        await Task.WhenAll(Account.States.Values.Select(o => Subscribe(o.Instrument)));

        return StatusEnum.Active;
      });
    }

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public override Task<ResponseModel<StatusEnum>> Disconnect()
    {
      return Response(() =>
      {
        connections?.ForEach(o => o?.Dispose());
        connections?.Clear();

        return Task.FromResult(StatusEnum.Active);
      });
    }

    /// <summary>
    /// Subscribe to data streams
    /// </summary>
    /// <param name="instrument"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<StatusEnum>> Subscribe(InstrumentModel instrument)
    {
      return await Response(async () =>
      {
        await Unsubscribe(instrument);

        Account.States.Get(instrument.Name).Instrument ??= instrument;

        var names = Account
          .States
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

        return StatusEnum.Active;
      });
    }

    /// <summary>
    /// Unsubscribe from data streams
    /// </summary>
    /// <param name="instrument"></param>
    /// <returns></returns>
    public override Task<ResponseModel<StatusEnum>> Unsubscribe(InstrumentModel instrument) => Response(() =>
    {
      return Task.FromResult(StatusEnum.Active);
    });

    /// <summary>
    /// Sync open balance, order, and positions 
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<IAccount>> GetAccount()
    {
      return await Response(async () =>
      {
        var num = Account.Descriptor;
        var account = await GetBalances(num);
        var orders = await GetOrders();
        var positions = await GetPositions();
        var openOrders = (orders.Data ?? []).Where(o => o.Status is OrderStatusEnum.Pending or OrderStatusEnum.Partitioned);

        Account.Balance = account.TotalEquity;
        Account.Orders = openOrders.GroupBy(o => o.Id).ToDictionary(o => o.Key, o => o.FirstOrDefault()).Concurrent();
        Account.Positions = (positions.Data ?? []).GroupBy(o => o.Name).ToDictionary(o => o.Key, o => o.FirstOrDefault()).Concurrent();
        Account.Positions.Values.ForEach(async o => await Subscribe(o.Instrument));

        return Account;
      });
    }

    /// <summary>
    /// Get latest quote
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<DomModel>> GetDom(ConditionModel criteria = null)
    {
      return await Response(async () =>
      {
        var name = criteria.Instrument.Name;
        var pointResponse = await GetQuotes([name], true);
        var point = Downstream.GetPrice(pointResponse?.Items?.FirstOrDefault());

        return new DomModel
        {
          Asks = [point],
          Bids = [point],
        };
      });
    }

    /// <summary>
    /// Get historical ticks
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override Task<ResponseModel<List<PointModel>>> GetPoints(ConditionModel criteria = null)
    {
      throw new NotImplementedException();
    }

    /// <summary>
    /// Get options
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<List<InstrumentModel>>> GetOptions(ConditionModel criteria = null)
    {
      return await Response(async () =>
      {
        var response = await GetOptionChain(criteria.Instrument.Name, criteria.MaxDate ?? criteria.MinDate);

        return response
          ?.Options
          ?.Select(o => Account.States.Get(o.Symbol).Instrument = Downstream.GetOption(o))
          ?.OrderBy(o => o.Derivative.ExpirationDate)
          ?.ThenBy(o => o.Derivative.Strike)
          ?.ThenBy(o => o.Derivative.Side)
          ?.ToList() ?? [];
      });
    }

    /// <summary>
    /// Get positions 
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<List<OrderModel>>> GetPositions(ConditionModel criteria = null)
    {
      return await Response(async () =>
      {
        var messages = await GetPositions(Account.Descriptor);
        var positions = messages?.Select(o => Downstream.GetPosition(o, Account))?.ToList();

        return positions ?? [];
      });
    }

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<List<OrderModel>>> GetOrders(ConditionModel criteria = null)
    {
      return await base.Response(async () =>
      {
        var messages = await GetOrders(Account.Descriptor);
        var orders = messages?.SelectMany(o => Downstream.GetOrder(o, Account))?.ToList();

        return orders ?? [];
      });
    }

    /// <summary>
    /// Create orders
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<OrderModel>> SendOrder(OrderModel order)
    {
      var preview = false;
      var message = null as OrderResponseMessage;
      var response = new ResponseModel<OrderModel>();

      if ((response.Errors = await SubscribeToOrder(order)).Count is 0)
      {
        Account.Orders[order.Id] = order;

        if (order.Orders.IsEmpty())
        {
          switch (order.Instrument.Type)
          {
            case InstrumentEnum.Shares: message = await SendEquityOrder(order, preview); break;
            case InstrumentEnum.Options: message = await SendOptionOrder(order, preview); break;
          }
        }
        else
        {
          var isBrace = order.Orders.Any(o => o.Instruction is InstructionEnum.Brace);
          var isCombo = order
            .Orders
            .Append(order)
            .Where(o => o?.Amount is not null)
            .Any(o => o?.Instrument?.Type is InstrumentEnum.Shares);

          switch (true)
          {
            case true when isBrace: message = await SendBraceOrder(order, preview); break;
            case true when isCombo: message = await SendComboOrder(order, preview); break;
            case true when isCombo is false: message = await SendGroupOrder(order, preview); break;
          }
        }

        if (Equals(message?.Status?.ToUpper(), "OK"))
        {
          Account.Orders.TryRemove(order.Id, out _);

          order.Id = $"{message?.Id}";
          order.Status = order.Type is OrderTypeEnum.Market ? OrderStatusEnum.Filled : OrderStatusEnum.Pending;

          if (order.Status is OrderStatusEnum.Filled)
          {
            Account.Positions[order.Name] = order;
          }
        }

        response.Data = order;
      }

      return response;
    }

    /// <summary>
    /// Cancel orders
    /// </summary>
    /// <param name="orders"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<List<OrderModel>>> ClearOrders(params OrderModel[] orders)
    {
      var response = new ResponseModel<List<OrderModel>> { Data = [] };

      foreach (var order in orders)
      {
        var o = await Response(async () => await ClearOrder(order.Id));

        response.Errors = [.. response.Errors.Concat(o.Errors)];
        response.Data = [.. response.Data.Append(order)];
      }

      response.Errors = [.. response.Errors.Concat((await GetAccount()).Errors)];

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
    public virtual async Task<T> Send<T>(string source, HttpMethod verb = null, Hashtable content = null, string token = null)
    {
      var message = $"{new UriBuilder(source)}"
        .WithHeader("Accept", "application/json")
        .WithHeader("Authorization", $"Bearer {token ?? Token}");

      var data = null as FormUrlEncodedContent;
      var sender = InstanceService<Service>.Instance;

      if (content is not null)
      {
        var map = new Dictionary<string, string>();

        foreach (DictionaryEntry o in content)
        {
          map[$"{o.Key}"] = $"{o.Value}";
        }

        //data = new StringContent(JsonSerializer.Serialize(content, sender.Options), Encoding.UTF8, "application/json");
        data = new FormUrlEncodedContent(map);
      }

      var response = await message
        .SendAsync(verb ?? HttpMethod.Get, data)
        .ConfigureAwait(false);

      var responseContent = await response
        .ResponseMessage
        .Content
        .ReadAsStringAsync()
        .ConfigureAwait(false);

      return JsonSerializer.Deserialize<T>(responseContent, sender.Options);
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
      var sender = InstanceService<Service>.Instance;
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
      var data = new byte[short.MaxValue];
      var source = new UriBuilder($"{StreamUri}{uri}");
      var cancellation = new CancellationTokenSource();

      await streamer.ConnectAsync(source.Uri, cancellation.Token);

      scheduler.Send(async () =>
      {
        while (streamer.State is WebSocketState.Open)
        {
          await Observe(async () =>
          {
            var streamResponse = await streamer.ReceiveAsync(new ArraySegment<byte>(data), cancellation.Token);
            var content = Encoding.UTF8.GetString(data, 0, streamResponse.Count);
            action(JsonNode.Parse(content));
          });
        }
      });
    }
  }
}
