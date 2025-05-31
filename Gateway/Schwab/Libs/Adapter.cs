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
using Terminal.Core.Services;
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
    /// Check active connections
    /// </summary>
    /// <returns></returns>
    public override bool IsConnected() => connections.Any();

    /// <summary>
    /// Connect
    /// </summary>
    public override async Task<ResponseModel<StatusEnum>> Connect()
    {
      var response = new ResponseModel<StatusEnum>();

      try
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

        await GetAccount([]);
        await GetConnection(streamer, scheduler);

        interval.Enabled = true;
        interval.Elapsed += async (sender, e) => await UpdateToken($"{DataUri}/v1/oauth/token");

        connections.Add(streamer);
        connections.Add(sender);
        connections.Add(interval);
        connections.Add(scheduler);

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
        var streamData = userData.Streamer.FirstOrDefault();

        await Unsubscribe(instrument);
        await SendStream(streamer, new StreamInputMessage
        {
          Requestid = ++counter,
          Service = ExternalMap.GetStreamingService(instrument),
          Command = "ADD",
          CustomerId = streamData.CustomerId,
          CorrelationId = $"{Guid.NewGuid()}",
          Parameters = new SrteamParamsMessage
          {
            Keys = instrument.Name,
            Fields = string.Join(",", Enumerable.Range(0, 10))
          }
        });
      }
      catch (Exception e)
      {
        response.Errors.Add(new ErrorModel { ErrorMessage = $"{e}" });
      }

      return response;
    }

    /// <summary>
    /// Subscribe to data streams
    /// </summary>
    /// <param name="instrument"></param>
    /// <param name="domType"></param>
    /// <returns></returns>
    public virtual async Task<ResponseModel<StatusEnum>> SubscribeToDom(InstrumentModel instrument, DomEnum domType)
    {
      var response = new ResponseModel<StatusEnum>
      {
        Data = StatusEnum.Active
      };

      var domName = "OPTIONS_BOOK";

      switch (domType)
      {
        case DomEnum.Ndx: domName = "NASDAQ_BOOK"; break;
        case DomEnum.Nyse: domName = "NYSE_BOOK"; break;
      }

      try
      {
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
        var props = new Hashtable
        {
          ["symbol"] = screener?.Instrument?.Name,
          ["toDate"] = $"{screener?.MaxDate:yyyy-MM-dd}",
          ["fromDate"] = $"{screener?.MinDate:yyyy-MM-dd}",
          ["strikeCount"] = screener?.Count ?? int.MaxValue

        }.Merge(criteria);

        var optionResponse = await Send<OptionChainMessage>($"{DataUri}/marketdata/v1/chains?{props}");

        response.Data = optionResponse
          ?.Data
          ?.PutExpDateMap
          ?.Concat(optionResponse?.Data?.CallExpDateMap)
          ?.SelectMany(dateMap => dateMap.Value.SelectMany(o => o.Value))
          ?.Select(option => InternalMap.GetOption(option, optionResponse.Data))
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
        var dom = Account.State[screener.Instrument.Name].Dom;

        if (dom.Bids.Count is not 0 && dom.Asks.Count is not 0)
        {
          response.Data = dom;
          return response;
        }

        var props = new Hashtable
        {
          ["indicative"] = false,
          ["symbols"] = screener.Instrument.Name,
          ["fields"] = "quote,fundamental,extended,reference,regular"

        }.Merge(criteria);

        var pointResponse = await Send<Dictionary<string, AssetMessage>>($"{DataUri}/marketdata/v1/quotes?{props}");
        var point = InternalMap.GetPrice(pointResponse.Data[props["symbols"]], screener.Instrument);

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
    public override async Task<ResponseModel<IList<PointModel>>> GetPoints(PointScreenerModel screener, Hashtable criteria)
    {
      var response = new ResponseModel<IList<PointModel>>();

      try
      {
        var props = new Hashtable
        {
          ["periodType"] = "day",
          ["period"] = 1,
          ["frequencyType"] = "minute",
          ["frequency"] = 1

        }.Merge(criteria);

        var pointResponse = await Send<BarsMessage>($"{DataUri}/marketdata/v1/pricehistory?{props}"); 

        response.Data = pointResponse
          .Data
          .Bars
          ?.Select(InternalMap.GetPrice)?.ToList() ?? [];
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
    public override async Task<ResponseModel<IList<OrderModel>>> CreateOrders(params OrderModel[] orders)
    {
      var response = new ResponseModel<IList<OrderModel>> { Data = [] };

      foreach (var order in orders)
      {
        try
        {
          response.Data.Add((await CreateOrder(order)).Data);
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
    public override async Task<ResponseModel<IList<OrderModel>>> DeleteOrders(params OrderModel[] orders)
    {
      var response = new ResponseModel<IList<OrderModel>> { Data = [] };

      foreach (var order in orders)
      {
        try
        {
          response.Data.Add((await DeleteOrder(order)).Data);
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
    /// Sync open balance, order, and positions 
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<IAccount>> GetAccount(Hashtable criteria)
    {
      var response = new ResponseModel<IAccount>();

      try
      {
        var accountProps = new Hashtable { ["fields"] = "positions" };
        var account = await Send<AccountsMessage>($"{DataUri}/trader/v1/accounts/{accountCode}?{accountProps.Query()}");
        var orders = await GetOrders(null, criteria);
        var positions = await GetPositions(null, criteria);

        Account.Balance = account.Data.AggregatedBalance.CurrentLiquidationValue;
        Account.Orders = orders.Data.GroupBy(o => o.Id).ToDictionary(o => o.Key, o => o.FirstOrDefault()).Concurrent();
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
        var dateFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
        var props = new Hashtable
        {
          ["maxResults"] = 50,
          ["toEnteredTime"] = DateTime.Now.AddDays(5).ToString(dateFormat),
          ["fromEnteredTime"] = DateTime.Now.AddDays(-100).ToString(dateFormat)

        }.Merge(criteria);

        var orders = await Send<OrderMessage[]>($"{DataUri}/trader/v1/accounts/{accountCode}/orders?{props}");

        response.Data = orders
          .Data
          ?.Where(o => o.CloseTime is null)
          ?.Select(InternalMap.GetOrder)
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
        var props = new Hashtable { ["fields"] = "positions" }.Merge(criteria);
        var account = await Send<AccountsMessage>($"{DataUri}/trader/v1/accounts/{accountCode}?{props}");

        response.Data = account
          .Data
          ?.SecuritiesAccount
          ?.Positions
          ?.Select(InternalMap.GetPosition)
          ?.ToList() ?? [];
      }
      catch (Exception e)
      {
        response.Errors = [new ErrorModel { ErrorMessage = $"{e}" }];
      }

      return response;
    }

    /// <summary>
    /// Sync open balance, order, and positions 
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    protected virtual async Task<ResponseModel<string>> GetAccountCode()
    {
      var response = new ResponseModel<string>();

      try
      {
        var accountNumbers = await Send<AccountNumberMessage[]>($"{DataUri}/trader/v1/accounts/accountNumbers");

        response.Data = accountNumbers.Data.First(o => Equals(o.AccountNumber, Account.Descriptor)).HashValue;
      }
      catch (Exception e)
      {
        response.Errors = [new ErrorModel { ErrorMessage = $"{e}" }];
      }

      return response;
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

      scheduler.Send(async () =>
      {
        while (streamer.State is WebSocketState.Open)
        {
          try
          {
            var data = new byte[short.MaxValue];
            var streamResponse = await streamer.ReceiveAsync(data, cancellation.Token);
            var content = $"{Encoding.Default.GetString(data).Trim(['\0', '[', ']'])}";
            var message = JsonNode.Parse(content);

            if (message["data"] is not null)
            {
              var streamItems = message["data"]
                .AsArray()
                .Select(o => o.Deserialize<StreamDataMessage>());

              var points = streamItems
                .Where(o => InternalMap.GetStreamPointType(o.Service) is not null)
                .ToList();

              var doms = streamItems
                .Where(o => InternalMap.GetStreamDomType(o.Service) is not null)
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
          }
          catch (Exception e)
          {
            InstanceService<MessageService>.Instance.OnMessage(new MessageModel<string> { Error = e });
          }
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
        var map = InternalMap.GetStreamMap(item.Service);

        foreach (var data in item.Content)
        {
          var instrumentName = $"{data.Get("key")}";
          var summary = Account.State[instrumentName];
          var instrument = summary.Instrument;
          var point = new PointModel();

          point.Time = DateTime.Now;
          point.Instrument = instrument;
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
          summary.PointGroups.Add(point, instrument.TimeFrame);
          instrument.Point = summary.PointGroups.Last();

          DataStream(new MessageModel<PointModel> { Next = instrument.Point });
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
    protected virtual async Task<ResponseModel<OrderModel>> CreateOrder(OrderModel order)
    {
      Account.Orders[order.Id] = order;

      await Subscribe(order.Transaction.Instrument);

      var exOrder = ExternalMap.GetOrder(Account, order);
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
    protected virtual async Task<ResponseModel<OrderModel>> DeleteOrder(OrderModel order)
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
