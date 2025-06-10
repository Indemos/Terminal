using Distribution.Services;
using Distribution.Stream;
using Distribution.Stream.Extensions;
using Schwab.Enums;
using Schwab.Mappers;
using Schwab.Messages;
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
using Dis = Distribution.Stream.Models;

namespace Schwab
{
  public class Adapter : Gateway
  {
    /// <summary>
    /// Request ID
    /// </summary>
    protected int counter;

    /// <summary>
    /// Encrypted account number
    /// </summary>
    protected string accountCode;

    /// <summary>
    /// HTTP client
    /// </summary>
    protected Service sender;

    /// <summary>
    /// Socket connection
    /// </summary>
    protected ClientWebSocket streamer;

    /// <summary>
    /// User preferences
    /// </summary>
    protected UserDataMessage userData;

    /// <summary>
    /// Disposable connections
    /// </summary>
    protected IList<IDisposable> connections;

    /// <summary>
    /// Data source
    /// </summary>
    public virtual string DataUri { get; set; }

    /// <summary>
    /// Streaming source
    /// </summary>
    public virtual string StreamUri { get; set; }

    /// <summary>
    /// Access token
    /// </summary>
    public virtual string AccessToken { get; set; }

    /// <summary>
    /// Refresh token
    /// </summary>
    public virtual string RefreshToken { get; set; }

    /// <summary>
    /// Client ID
    /// </summary>
    public virtual string ClientId { get; set; }

    /// <summary>
    /// Client secret
    /// </summary>
    public virtual string ClientSecret { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public Adapter()
    {
      DataUri = "https://api.schwabapi.com";
      StreamUri = "wss://streamer-api.schwab.com/ws";

      counter = 0;
      connections = [];
    }

    /// <summary>
    /// Connect
    /// </summary>
    public override async Task<ResponseModel<StatusEnum>> Connect()
    {
      return await Response(async () =>
      {
        await Disconnect();

        var sender = new Service();
        var streamer = new ClientWebSocket();
        var scheduler = new ScheduleService();
        var interval = new System.Timers.Timer(TimeSpan.FromMinutes(1));

        this.sender = sender;
        this.streamer = streamer;

        await UpdateToken($"{DataUri}/v1/oauth/token");

        accountCode = (await GetAccountCode()).Data;

        await GetAccount();
        await GetConnection(streamer, scheduler);

        interval.Enabled = true;
        interval.Elapsed += async (sender, e) => await UpdateToken($"{DataUri}/v1/oauth/token");

        connections.Add(streamer);
        connections.Add(sender);
        connections.Add(interval);
        connections.Add(scheduler);

        await Task.WhenAll(Account.State.Values.Select(o => Subscribe(o.Instrument)));

        return StatusEnum.Active;
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
        var streamData = userData.Streamer.FirstOrDefault();

        await Unsubscribe(instrument);

        Account.State[instrument.Name] = Account.State.Get(instrument.Name) ?? new StateModel();
        Account.State[instrument.Name].Instrument ??= instrument;

        await SendStream(streamer, new StreamInputMessage
        {
          Requestid = ++counter,
          Service = Upstream.GetStreamingService(instrument),
          Command = "ADD",
          CustomerId = streamData.CustomerId,
          CorrelationId = $"{Guid.NewGuid()}",
          Parameters = new SrteamParamsMessage
          {
            Keys = instrument.Name,
            Fields = string.Join(",", Enumerable.Range(0, 10))
          }
        });

        return StatusEnum.Active;
      });
    }

    /// <summary>
    /// Subscribe to data streams
    /// </summary>
    /// <param name="instrument"></param>
    /// <param name="domType"></param>
    /// <returns></returns>
    public virtual async Task<ResponseModel<StatusEnum>> SubscribeToDom(InstrumentModel instrument, DomEnum domType)
    {
      return await Response(async () =>
      {
        var domName = "OPTIONS_BOOK";

        switch (domType)
        {
          case DomEnum.Ndx: domName = "NASDAQ_BOOK"; break;
          case DomEnum.Nyse: domName = "NYSE_BOOK"; break;
        }

        var streamData = userData.Streamer.FirstOrDefault();

        await Unsubscribe(instrument);
        await SendStream(streamer, new StreamInputMessage
        {
          Requestid = ++counter,
          Service = domName,
          Command = "ADD",
          CustomerId = streamData.CustomerId,
          CorrelationId = $"{Guid.NewGuid()}",
          Parameters = new SrteamParamsMessage
          {
            Keys = instrument.Name,
            Fields = string.Join(",", Enumerable.Range(0, 3))
          }
        });

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
    /// Unsubscribe from data streams
    /// </summary>
    /// <param name="instrument"></param>
    /// <returns></returns>
    public override Task<ResponseModel<StatusEnum>> Unsubscribe(InstrumentModel instrument) => Response(() =>
    {
      return Task.FromResult(StatusEnum.Active);
    });

    /// <summary>
    /// Get options
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<List<InstrumentModel>>> GetOptions(ConditionModel criteria = null)
    {
      return await Response(async () =>
      {
        var props = new Hashtable
        {
          ["symbol"] = criteria?.Instrument?.Name,
          ["toDate"] = $"{criteria?.MaxDate:yyyy-MM-dd}",
          ["fromDate"] = $"{criteria?.MinDate:yyyy-MM-dd}",
          ["strikeCount"] = byte.MaxValue

        }.Merge(criteria);

        var optionResponse = await Send<OptionChainMessage>($"{DataUri}/marketdata/v1/chains?{props}");

        return optionResponse
          ?.Data
          ?.PutExpDateMap
          ?.Concat(optionResponse?.Data?.CallExpDateMap)
          ?.SelectMany(dateMap => dateMap.Value.SelectMany(o => o.Value))
          ?.Select(option => Downstream.GetOption(option, optionResponse.Data))
          ?.OrderBy(o => o.Derivative.ExpirationDate)
          ?.ThenBy(o => o.Derivative.Strike)
          ?.ThenBy(o => o.Derivative.Side)
          ?.ToList() ?? [];
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
        var instrument = criteria.Instrument;
        var dom = Account.State[instrument.Name].Dom;

        if (dom.Bids.Count is not 0 && dom.Asks.Count is not 0)
        {
          return dom;
        }

        var props = new Hashtable
        {
          ["indicative"] = false,
          ["symbols"] = instrument.Name,
          ["fields"] = "quote,fundamental,extended,reference,regular"

        }.Merge(criteria);

        var pointResponse = await Send<Dictionary<string, AssetMessage>>($"{DataUri}/marketdata/v1/quotes?{props}");
        var point = Downstream.GetPrice(pointResponse.Data[props["symbols"]], instrument);

        return new DomModel
        {
          Asks = [point],
          Bids = [point]
        };
      });
    }

    /// <summary>
    /// Get historical ticks
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<List<PointModel>>> GetPoints(ConditionModel criteria = null)
    {
      return await Response(async () =>
      {
        var props = new Hashtable
        {
          ["periodType"] = "day",
          ["period"] = 1,
          ["frequencyType"] = "minute",
          ["frequency"] = 1

        }.Merge(criteria);

        var pointResponse = await Send<BarsMessage>($"{DataUri}/marketdata/v1/pricehistory?{props}");

        return pointResponse
          .Data
          .Bars
          .Select(Downstream.GetPrice)?.ToList() ?? [];
      });
    }

    /// <summary>
    /// Create orders
    /// </summary>
    /// <param name="orders"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<List<OrderModel>>> SendOrders(params OrderModel[] orders)
    {
      var response = new ResponseModel<List<OrderModel>> { Data = [] };

      foreach (var order in orders)
      {
        var o = await Response(async () => await SendOrder(order));

        response.Errors = [.. response.Errors.Concat(o.Errors)];
        response.Data = [.. response.Data.Append(order)];
      }

      response.Errors = [.. response.Errors.Concat((await GetAccount()).Errors)];

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
        var o = await Response(async () => await ClearOrder(order));

        response.Errors = [.. response.Errors.Concat(o.Errors)];
        response.Data = [.. response.Data.Append(order)];
      }

      response.Errors = [.. response.Errors.Concat((await GetAccount()).Errors)];

      return response;
    }

    /// <summary>
    /// Sync open balance, order, and positions 
    /// </summary>
    public override async Task<ResponseModel<IAccount>> GetAccount()
    {
      return await Response(async () =>
      {
        var accountProps = new Hashtable { ["fields"] = "positions" };
        var account = await Send<AccountsMessage>($"{DataUri}/trader/v1/accounts/{accountCode}?{accountProps.Query()}");
        var orders = await GetOrders();
        var positions = await GetPositions();

        Account.Balance = account.Data.AggregatedBalance.CurrentLiquidationValue;
        Account.Orders = orders.Data.GroupBy(o => o.Id).ToDictionary(o => o.Key, o => o.FirstOrDefault()).Concurrent();
        Account.Positions = positions.Data.GroupBy(o => o.Name).ToDictionary(o => o.Key, o => o.FirstOrDefault()).Concurrent();
        Account.Positions.Values.ForEach(async o => await Subscribe(o.Transaction.Instrument));

        return Account;
      });
    }

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<List<OrderModel>>> GetOrders(ConditionModel criteria = null)
    {
      return await Response(async () =>
      {
        var dateFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
        var props = new Hashtable
        {
          ["maxResults"] = 50,
          ["toEnteredTime"] = DateTime.Now.AddDays(5).ToString(dateFormat),
          ["fromEnteredTime"] = DateTime.Now.AddDays(-100).ToString(dateFormat)

        }.Merge(criteria);

        var orders = await Send<OrderMessage[]>($"{DataUri}/trader/v1/accounts/{accountCode}/orders?{props}");

        return orders
          .Data
          .Where(o => o.CloseTime is null)
          .Select(Downstream.GetOrder)
          .ToList() ?? [];
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
        var props = new Hashtable { ["fields"] = "positions" }.Merge(criteria);
        var account = await Send<AccountsMessage>($"{DataUri}/trader/v1/accounts/{accountCode}?{props}");

        return account
          ?.Data
          ?.SecuritiesAccount
          ?.Positions
          ?.Select(Downstream.GetPosition)
          ?.ToList() ?? [];
      });
    }

    /// <summary>
    /// Sync open balance, order, and positions 
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    protected virtual async Task<ResponseModel<string>> GetAccountCode()
    {
      return await Response(async () =>
      {
        var accountNumbers = await Send<AccountNumberMessage[]>($"{DataUri}/trader/v1/accounts/accountNumbers");

        return accountNumbers
          .Data
          .First(o => Equals(o.AccountNumber, Account.Descriptor)).HashValue;
      });
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
    /// Read response from web socket
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="streamer"></param>
    /// <param name="source"></param>
    /// <returns></returns>
    protected virtual async Task<T> ReceiveStream<T>(ClientWebSocket streamer, CancellationTokenSource source = null)
    {
      var cancellation = source?.Token ?? CancellationToken.None;
      var data = new byte[short.MaxValue];
      var response = await streamer.ReceiveAsync(data, cancellation);
      var message = Encoding.UTF8.GetString(data, 0, response.Count);

      return JsonSerializer.Deserialize<T>(message, sender.Options);
    }

    /// <summary>
    /// Web socket stream
    /// </summary>
    /// <param name="streamer"></param>
    /// <param name="scheduler"></param>
    /// <returns></returns>
    protected virtual async Task<ClientWebSocket> GetConnection(ClientWebSocket streamer, ScheduleService scheduler)
    {
      var source = new UriBuilder(StreamUri);
      var cancellation = new CancellationTokenSource();
      var userData = await GetUserData();
      var streamData = userData.Data.Streamer.FirstOrDefault();

      await streamer.ConnectAsync(source.Uri, cancellation.Token);
      await SendStream(streamer, new StreamInputMessage
      {
        Service = "ADMIN",
        Command = "LOGIN",
        Requestid = ++counter,
        CustomerId = streamData.CustomerId,
        CorrelationId = $"{Guid.NewGuid()}",
        Parameters = new StreamLoginMessage
        {
          Channel = streamData.Channel,
          FunctionId = streamData.FunctionId,
          Authorization = AccessToken
        }
      });

      var adminResponse = await ReceiveStream<StreamLoginResponseMessage>(streamer);
      var adminCode = adminResponse.Response.FirstOrDefault().Content.Code;
      var data = new byte[short.MaxValue];

      scheduler.Send(async () =>
      {
        while (streamer.State is WebSocketState.Open)
        {
          await Observe(async () =>
          {
            var streamResponse = await streamer.ReceiveAsync(new ArraySegment<byte>(data), cancellation.Token);
            var content = Encoding.UTF8.GetString(data, 0, streamResponse.Count);
            var message = JsonNode.Parse(content);

            if (message["data"] is not null)
            {
              var streamItems = message["data"]
                .AsArray()
                .Select(o => o.Deserialize<StreamDataMessage>());

              var points = streamItems
                .Where(o => Downstream.GetStreamPointType(o.Service) is not null)
                .ToList();

              var doms = streamItems
                .Where(o => Downstream.GetStreamDomType(o.Service) is not null)
                .ToList();

              if (points.Count is not 0)
              {
                OnPoint(points);
              }

              if (doms.Count is not 0)
              {
                OnDom(doms);
              }
            }
          });
        }
      });

      return streamer;
    }

    /// <summary>
    /// Process quote from the stream
    /// </summary>
    /// <param name="items"></param>
    protected virtual void OnPoint(IEnumerable<StreamDataMessage> items)
    {
      static double? parse(string o, double? origin) => double.TryParse(o, out var num) ? num : origin;

      foreach (var item in items)
      {
        var map = Downstream.GetStreamMap(item.Service);

        foreach (var data in item.Content)
        {
          var instrumentName = $"{data.Get("key")}";
          var summary = Account.State[instrumentName];
          var point = new PointModel();

          point.Time = DateTime.Now;
          point.Instrument = summary.Instrument;
          point.Bid = parse($"{data.Get(map.Get("Bid Price"))}", point.Bid);
          point.Ask = parse($"{data.Get(map.Get("Ask Price"))}", point.Ask);
          point.BidSize = parse($"{data.Get(map.Get("Bid Size"))}", point.BidSize);
          point.AskSize = parse($"{data.Get(map.Get("Ask Size"))}", point.AskSize);
          point.Last = parse($"{data.Get(map.Get("Last Price"))}", point.Last);

          point.Last = point.Last is 0 or null ? point.Bid ?? point.Ask : point.Last;
          point.Bid ??= point.Last;
          point.Ask ??= point.Last;

          if (point.Bid is null || point.Ask is null || point.Last is null)
          {
            return;
          }

          summary.Points.Add(point);
          summary.PointGroups.Add(point, summary.Instrument.TimeFrame);
          summary.Instrument.Point = summary.PointGroups.Last();

          DataStream(new MessageModel<PointModel> { Next = summary.Instrument.Point });
        }
      }
    }

    /// <summary>
    /// Process quote from the stream
    /// </summary>
    /// <param name="items"></param>
    protected virtual void OnDom(IEnumerable<StreamDataMessage> items)
    {
      foreach (var item in items)
      {
        var map = StreamDomMap.Map;

        foreach (var data in item.Content)
        {
          var instrumentName = $"{data.Get("key")}";
          var summary = Account.State[instrumentName];
          var instrument = summary.Instrument;
          var dom = Account.State.Get(instrumentName).Dom = new();
          var bids = data.Get(map.Get("Bid Side Levels"));
          var asks = data.Get(map.Get("Ask Side Levels"));

          dom.Bids = [.. bids.AsArray().Select(node =>
          {
            var o = node.AsObject();
            var point = new PointModel
            {
              Last = o.TryGetPropertyValue("0", out var price) ? double.Parse($"{price}") : 0,
              Volume = o.TryGetPropertyValue("1", out var volume) ? double.Parse($"{volume}") : 0
            };

            return point;

          }).OrderBy(o => o.Last)];

          dom.Asks = [.. asks.AsArray().Select(node =>
          {
            var o = node.AsObject();
            var point = new PointModel
            {
              Last = o.TryGetPropertyValue("0", out var price) ? double.Parse($"{price}") : 0,
              Volume = o.TryGetPropertyValue("1", out var volume) ? double.Parse($"{volume}") : 0
            };

            return point;

          }).OrderBy(o => o.Last)];
        }
      }
    }

    /// <summary>
    /// Send data to the API
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="verb"></param>
    /// <param name="content"></param>
    /// <returns></returns>
    protected virtual async Task<Dis.ResponseModel<T>> Send<T>(string source, HttpMethod verb = null, object content = null)
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
      message.Headers.Add("Authorization", $"Bearer {AccessToken}");

      var response = await sender.Send<T>(message, sender.Options);

      if (response?.Message?.IsSuccessStatusCode is false)
      {
        throw new HttpRequestException(response.Message.ReasonPhrase, null, response.Message.StatusCode);
      }

      return response;
    }

    /// <summary>
    /// Refresh token
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <returns></returns>
    protected virtual async Task UpdateToken(string source)
    {
      var props = new Dictionary<string, string>
      {
        ["grant_type"] = "refresh_token",
        ["refresh_token"] = RefreshToken
      };

      var uri = new UriBuilder(source);
      var content = new FormUrlEncodedContent(props);
      var message = new HttpRequestMessage();
      var basicToken = Encoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}");

      message.Content = content;
      message.RequestUri = uri.Uri;
      message.Method = HttpMethod.Post;
      message.Headers.Add("Authorization", $"Basic {Convert.ToBase64String(basicToken)}");

      var response = await sender.Send<ScopeMessage>(message, sender.Options);

      if (response.Data is not null)
      {
        AccessToken = response.Data.AccessToken;
        RefreshToken = response.Data.RefreshToken;
      }
    }

    /// <summary>
    /// Refresh token
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <returns></returns>
    protected async Task<ResponseModel<UserDataMessage>> GetUserData()
    {
      var response = new ResponseModel<UserDataMessage>();
      var userResponse = await Send<UserDataMessage>($"{DataUri}/trader/v1/userPreference");

      if (string.IsNullOrEmpty(userResponse.Error) is false)
      {
        response.Errors = [new ErrorModel { ErrorMessage = userResponse.Error }];
      }

      userData = response.Data = userResponse.Data;

      return response;
    }

    /// <summary>
    /// Send order
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    protected virtual async Task<ResponseModel<OrderModel>> SendOrder(OrderModel order)
    {
      Account.Orders[order.Id] = order;

      await Subscribe(order.Transaction.Instrument);

      var exOrder = Upstream.GetOrder(Account, order);
      var response = new ResponseModel<OrderModel>();
      var exResponse = await Send<OrderMessage>($"{DataUri}/trader/v1/accounts/{accountCode}/orders", HttpMethod.Post, exOrder);

      if (exResponse.Message.Headers.TryGetValues("Location", out var orderData))
      {
        var orderItem = orderData.First();

        response.Data = order;
        response.Data.Transaction.Status = OrderStatusEnum.Filled;
        response.Data.Transaction.Id = $"{orderItem[(orderItem.LastIndexOf('/') + 1)..]}";
      }

      if (string.IsNullOrEmpty(response?.Data?.Transaction?.Id))
      {
        response.Errors.Add(new ErrorModel { ErrorMessage = $"{exResponse.Message.StatusCode}" });
      }

      return response;
    }

    /// <summary>
    /// Cancel order
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    protected virtual async Task<ResponseModel<OrderModel>> ClearOrder(OrderModel order)
    {
      var response = new ResponseModel<OrderModel>();
      var exResponse = await Send<OrderMessage>($"{DataUri}/trader/v1/accounts/{accountCode}/orders/{order.Transaction.Id}", HttpMethod.Delete);

      if ((int)exResponse.Message.StatusCode >= 400)
      {
        response.Errors.Add(new ErrorModel { ErrorMessage = $"{exResponse.Message.StatusCode}" });
        return response;
      }

      response.Data = order;
      response.Data.Transaction.Status = OrderStatusEnum.Canceled;

      return response;
    }
  }
}
