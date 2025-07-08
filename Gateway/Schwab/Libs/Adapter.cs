using Distribution.Services;
using Distribution.Stream;
using Flurl;
using Flurl.Http;
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

        var streamer = new ClientWebSocket();
        var scheduler = new ScheduleService();
        var interval = new System.Timers.Timer(TimeSpan.FromMinutes(1));

        this.streamer = streamer;

        await UpdateToken($"{DataUri}/v1/oauth/token");

        accountCode = (await GetAccountCode()).Data;

        await GetConnection(streamer, scheduler);
        await GetAccount();

        interval.Enabled = true;
        interval.Elapsed += async (sender, e) => await UpdateToken($"{DataUri}/v1/oauth/token");

        connections.Add(streamer);
        connections.Add(interval);
        connections.Add(scheduler);

        await Task.WhenAll(Account.States.Values.Select(o => Subscribe(o.Instrument)));

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

        Account.States.Get(instrument.Name).Instrument ??= instrument;

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
          ["symbol"] = criteria?.Instrument?.Name ?? criteria.Get("symbol"),
          ["toDate"] = $"{criteria?.MaxDate ?? criteria.Get("toDate"):yyyy-MM-dd}",
          ["fromDate"] = $"{criteria?.MinDate ?? criteria.Get("fromDate"):yyyy-MM-dd}",
          ["strikeCount"] = criteria.Get("strikeCount") ?? byte.MaxValue
        };

        var optionResponse = await Send<OptionChainMessage>($"{DataUri}/marketdata/v1/chains".SetQueryParams(props));

        return optionResponse
          ?.PutExpDateMap
          ?.Concat(optionResponse?.CallExpDateMap)
          ?.SelectMany(dateMap => dateMap.Value.SelectMany(o => o.Value))
          ?.Select(option => Downstream.GetOption(option, optionResponse))
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
        var dom = Account.States.Get(instrument.Name).Dom;

        if (dom.Bids.Count is not 0 && dom.Asks.Count is not 0)
        {
          return dom;
        }

        var props = new Hashtable
        {
          ["indicative"] = false,
          ["symbols"] = instrument.Name,
          ["fields"] = "quote,fundamental,extended,reference,regular"
        };

        var pointResponse = await Send<Dictionary<string, AssetMessage>>($"{DataUri}/marketdata/v1/quotes".SetQueryParams(props));
        var point = Downstream.GetPrice(pointResponse[instrument.Name], instrument);

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

        };

        var pointResponse = await Send<BarsMessage>($"{DataUri}/marketdata/v1/pricehistory".SetQueryParams(props));

        return pointResponse
          .Bars
          .Select(Downstream.GetPrice)?.ToList() ?? [];
      });
    }

    /// <summary>
    /// Create orders
    /// </summary>
    /// <param name="orders"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<OrderModel>> SendOrder(OrderModel order)
    {
      var response = new ResponseModel<OrderModel>();

      if ((response.Errors = await SubscribeToOrder(order)).Count is 0)
      {
        Account.Orders[order.Id] = order;

        var exOrder = Upstream.GetOrder(Account, order);
        var map = new Dictionary<string, IEnumerable<string>> { ["Location"] = null };
        var exResponse = await Send<OrderMessage>($"{DataUri}/trader/v1/accounts/{accountCode}/orders", HttpMethod.Post, exOrder, map);

        if (map["Location"] is not null)
        {
          var orderItem = map["Location"].First();

          response.Data = order;
          response.Data.Status = OrderStatusEnum.Filled;
          response.Data.Id = $"{orderItem[(orderItem.LastIndexOf('/') + 1)..]}";
        }
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
        var account = await Send<AccountsMessage>($"{DataUri}/trader/v1/accounts/{accountCode}".SetQueryParams(accountProps));
        var orders = await GetOrders();
        var positions = await GetPositions();

        Account.Balance = account.AggregatedBalance.CurrentLiquidationValue;
        Account.Orders = orders.Data.GroupBy(o => o.Id).ToDictionary(o => o.Key, o => o.FirstOrDefault()).Concurrent();
        Account.Positions = positions.Data.GroupBy(o => o.Instrument.Name).ToDictionary(o => o.Key, o => o.FirstOrDefault()).Concurrent();
        Account.Positions.Values.ForEach(async o => await Subscribe(o.Instrument));

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
        };

        var orders = await Send<OrderMessage[]>($"{DataUri}/trader/v1/accounts/{accountCode}/orders".SetQueryParams(props));

        return orders
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
        var props = new Hashtable { ["fields"] = "positions" };
        var account = await Send<AccountsMessage>($"{DataUri}/trader/v1/accounts/{accountCode}".SetQueryParams(props));

        return account
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

        return accountNumbers.First(o => Equals(o.AccountNumber, Account.Descriptor)).HashValue;
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
      var sender = InstanceService<Service>.Instance;

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
                OnPoint(points, content);
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
    protected virtual void OnPoint(IEnumerable<StreamDataMessage> items, string content)
    {
      static double? parse(string o, double? origin) => double.TryParse(o, out var num) ? num : origin;

      foreach (var item in items)
      {
        var map = Downstream.GetStreamMap(item.Service);

        foreach (var data in item.Content)
        {
          var instrumentName = $"{data.Get("key")}";
          var summary = Account.States.Get(instrumentName);
          var point = new PointModel();

          point.Account = Account;
          point.Time = DateTime.Now;
          point.TimeFrame = summary.TimeFrame;
          point.Name = summary.Instrument.Name;
          point.Bid = parse($"{data.Get(map.Get("Bid Price"))}", point.Bid);
          point.Ask = parse($"{data.Get(map.Get("Ask Price"))}", point.Ask);
          point.BidSize = parse($"{data.Get(map.Get("Bid Size"))}", point.BidSize);
          point.AskSize = parse($"{data.Get(map.Get("Ask Size"))}", point.AskSize);
          point.Last = parse($"{data.Get(map.Get("Last Price"))}", point.Last);
          point.Last = point.Last is 0 or null ? point.Bid ?? point.Ask : point.Last;

          if (point.Bid is null || point.Ask is null)
          {
            continue;
          }

          summary.Points.Add(point);
          summary.PointGroups.Add(point, summary.TimeFrame);
          summary.Instrument.Point = summary.PointGroups.Last();

          Stream(new MessageModel<PointModel> { Next = summary.Instrument.Point });
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
          var summary = Account.States.Get(instrumentName);
          var instrument = summary.Instrument;
          var dom = Account.States.Get(instrumentName).Dom = new();
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
    /// <param name="responseHeaders"></param>
    /// <returns></returns>
    protected virtual async Task<T> Send<T>(string source, HttpMethod verb = null, object content = null, Dictionary<string, IEnumerable<string>> responseHeaders = null)
    {
      var message = $"{new UriBuilder(source)}"
        .WithHeader("Accept", "application/json")
        .WithHeader("Authorization", $"Bearer {AccessToken}");

      var data = null as StringContent;
      var sender = InstanceService<Service>.Instance;

      if (content is not null)
      {
        data = new StringContent(JsonSerializer.Serialize(content, sender.Options), Encoding.UTF8, "application/json");
      }

      var response = await message
        .SendAsync(verb ?? HttpMethod.Get, data)
        .ConfigureAwait(false);

      foreach (var o in responseHeaders ?? [])
      {
        responseHeaders[o.Key] = response.ResponseMessage.Headers.TryGetValues(o.Key, out var v) ? v : null;
      }

      var responseContent = await response
        .ResponseMessage
        .Content
        .ReadAsStringAsync()
        .ConfigureAwait(false);

      return JsonSerializer.Deserialize<T>(responseContent, sender.Options);
    }

    /// <summary>
    /// Refresh token
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <returns></returns>
    protected virtual async Task UpdateToken(string source)
    {
      var message = $"{new UriBuilder(source)}"
        .WithHeader("Accept", "application/json")
        .WithHeader("Authorization", $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}"))}");

      var content = new Hashtable
      {
        ["grant_type"] = "refresh_token",
        ["refresh_token"] = RefreshToken
      };

      var response = await message
        .PostUrlEncodedAsync(content)
        .ConfigureAwait(false);

      if (response.ResponseMessage.IsSuccessStatusCode is false)
      {
        throw new HttpRequestException(response.ResponseMessage.ReasonPhrase, null, response.ResponseMessage.StatusCode);
      }

      var sender = InstanceService<Service>.Instance;
      var responseContent = await response
        .ResponseMessage
        .Content
        .ReadAsStringAsync()
        .ConfigureAwait(false);

      var scope = JsonSerializer.Deserialize<ScopeMessage>(responseContent, sender.Options);

      if (scope is not null)
      {
        AccessToken = scope.AccessToken;
        RefreshToken = scope.RefreshToken;
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

      userData = response.Data = userResponse;

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
      var exResponse = await Send<OrderMessage>($"{DataUri}/trader/v1/accounts/{accountCode}/orders/{order.Id}", HttpMethod.Delete);

      response.Data = order;
      response.Data.Status = OrderStatusEnum.Canceled;

      return response;
    }
  }
}
