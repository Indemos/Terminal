using Distribution.Services;
using Distribution.Stream;
using Distribution.Stream.Extensions;
using Schwab.Mappers;
using Schwab.Messages;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Extensions;
using Terminal.Core.Models;
using Terminal.Core.Services;

namespace Schwab
{
  public class Adapter : Gateway
  {
    /// <summary>
    /// Encrypted account number
    /// </summary>
    protected string _accountCode;

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
    /// Data endpoint
    /// </summary>
    public virtual string DataUri { get; set; }

    /// <summary>
    /// Streaming endpoint
    /// </summary>
    public virtual string StreamUri { get; set; }

    /// <summary>
    /// Tokens
    /// </summary>
    public virtual ScopeMessage Scope { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public Adapter()
    {
      DataUri = "https://api.schwabapi.com";

      _connections = [];
    }

    /// <summary>
    /// Connect
    /// </summary>
    public override async Task<ResponseModel<StatusEnum>> Connect()
    {
      var response = new ResponseModel<StatusEnum>();

      try
      {
        await Disconnect();

        _sender = new Service();

        await GetAccount([]);
        await UpdateToken("/v1/oauth/token");

        var interval = new Timer(TimeSpan.FromMinutes(1));

        interval.Enabled = true;
        interval.Elapsed += async (sender, e) => await UpdateToken("/v1/oauth/token");

        _connections.Add(_sender);
        _connections.Add(_streamer);
        _connections.Add(interval);

        Account.Instruments.ForEach(async o => await Subscribe(o.Value));

        response.Data = StatusEnum.Success;
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
      var response = new ResponseModel<StatusEnum>();

      try
      {
        await Unsubscribe(instrument);

        response.Data = StatusEnum.Success;
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
        _connections?.ForEach(o => o?.Dispose());
        _connections?.Clear();

        response.Data = StatusEnum.Success;
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
        Data = StatusEnum.Success
      };

      return Task.FromResult(response);
    }

    /// <summary>
    /// Get options
    /// </summary>
    /// <param name="screener"></param>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<IList<InstrumentModel>>> GetOptions(OptionScreenerModel screener, Hashtable criteria)
    {
      var response = new ResponseModel<IList<InstrumentModel>>();

      try
      {
        var props = new Hashtable
        {
          ["symbol"] = screener.Name,
          ["toDate"] = $"{screener.MaxDate:yyyy-MM-dd}",
          ["fromDate"] = $"{screener.MinDate:yyyy-MM-dd}",
          ["strikeCount"] = screener.Count ?? int.MaxValue

        }.Merge(criteria);

        var optionResponse = await SendData<OptionChainMessage>($"/marketdata/v1/chains?{props}");

        if (optionResponse.Data is not null)
        {
          response.Data = optionResponse
            .Data
            .PutExpDateMap
            ?.Concat(optionResponse.Data.CallExpDateMap)
            ?.SelectMany(dateMap => dateMap.Value.SelectMany(o => o.Value))
            ?.Select(option => InternalMap.GetOption(option, optionResponse.Data))
            ?.ToList() ?? [];
        }
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
    public override async Task<ResponseModel<DomModel>> GetDom(DomScreenerModel screener, Hashtable criteria)
    {
      var response = new ResponseModel<DomModel>();

      try
      {
        var props = new Hashtable
        {
          ["symbols"] = screener.Name,
          ["indicative"] = false,
          ["fields"] = "quote,fundamental,extended,reference,regular"

        }.Merge(criteria);

        var pointResponse = await SendData<Dictionary<string, AssetMessage>>($"/marketdata/v1/quotes?{props}");

        response.Data = InternalMap.GetDom(pointResponse.Data[props["symbols"]]);
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
          ["frequency"] = 1,
          ["symbol"] = screener.Name

        }.Merge(criteria);

        var pointResponse = await SendData<BarsMessage>($"/marketdata/v1/pricehistory?{props}");

        response.Data = pointResponse
          .Data
          .Bars
          ?.Select(InternalMap.GetBar)?.ToList() ?? [];
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
      var response = new ResponseModel<IList<OrderModel>>();

      foreach (var order in orders)
      {
        response.Data.Add((await CreateOrder(order)).Data);
      }

      return response;
    }

    /// <summary>
    /// Cancel orders
    /// </summary>
    /// <param name="orders"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<IList<OrderModel>>> DeleteOrders(params OrderModel[] orders)
    {
      var response = new ResponseModel<IList<OrderModel>>();

      foreach (var order in orders)
      {
        response.Data.Add((await DeleteOrder(order)).Data);
      }

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
        var accountNumbers = await SendData<AccountNumberMessage[]>("/trader/v1/accounts/accountNumbers");

        _accountCode = accountNumbers.Data.First(o => Equals(o.AccountNumber, Account.Descriptor)).HashValue;

        var account = await SendData<AccountsMessage>($"/trader/v1/accounts/{_accountCode}?{accountProps.Query()}");
        var orders = await GetOrders(null, criteria);

        Account.Balance = account.Data.AggregatedBalance.CurrentLiquidationValue;
        Account.ActivePositions = new ConcurrentQueue<PositionModel>(account
          .Data
          .SecuritiesAccount
          .Positions
          .Select(InternalMap.GetPosition));

        Account
          .ActiveOrders
          .Select(o => o.Transaction.Instrument.Name)
          .Concat(Account.ActivePositions.Select(o => o.Order.Transaction.Instrument.Name))
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

        var items = await SendData<OrderMessage[]>($"/trader/v1/accounts/{_accountCode}/orders?{props}");

        response.Data = [.. items.Data.Where(o => o.CloseTime is null).Select(InternalMap.GetOrder)];
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
    public override async Task<ResponseModel<IList<PositionModel>>> GetPositions(PositionScreenerModel screener, Hashtable criteria)
    {
      var response = new ResponseModel<IList<PositionModel>>();

      try
      {
        var props = new Hashtable { ["fields"] = "positions" }.Merge(criteria);
        var account = await SendData<AccountsMessage>($"/trader/v1/accounts/{_accountCode}?{props}");

        response.Data = [.. account
          .Data
          .SecuritiesAccount
          .Positions
          .Select(InternalMap.GetPosition)];
      }
      catch (Exception e)
      {
        response.Errors = [new ErrorModel { ErrorMessage = $"{e}" }];
      }

      return response;
    }

    /// <summary>
    /// Send data to the API
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="verb"></param>
    /// <param name="query"></param>
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
        case true when Equals(message.Method, HttpMethod.Patch):
          message.Content = new StringContent(JsonSerializer.Serialize(content));
          break;
      }

      message.RequestUri = uri.Uri;
      message.Headers.Add("Authorization", $"Bearer {Scope.AccessToken}");

      return await _sender.Send<T>(message, _sender.Options);
    }
    /// <summary>
    /// Refresh token
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <returns></returns>
    protected virtual async Task UpdateToken(string source)
    {
      try
      {
        var props = new Dictionary<string, string>
        {
          ["grant_type"] = "refresh_token",
          ["refresh_token"] = Scope.RefreshToken
        };

        var uri = new UriBuilder(DataUri + source);
        var content = new FormUrlEncodedContent(props);
        var message = new HttpRequestMessage();
        var basicToken = Encoding.UTF8.GetBytes($"{Scope.ConsumerKey}:{Scope.ConsumerSecret}");

        message.Content = content;
        message.RequestUri = uri.Uri;
        message.Method = HttpMethod.Post;
        message.Headers.Add("Authorization", $"Basic {Convert.ToBase64String(basicToken)}");

        var response = await _sender.Send<ScopeMessage>(message, _sender.Options);

        if (response.Data is not null)
        {
          Scope.AccessToken = response.Data.AccessToken;
          Scope.RefreshToken = response.Data.RefreshToken;
        }
      }
      catch (Exception e)
      {
        InstanceService<MessageService>.Instance.OnMessage($"{e}");
      }
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
        var exResponse = await SendData<OrderMessage>($"/trader/v1/accounts/{_accountCode}/orders", HttpMethod.Post, exOrder);

        inResponse.Data = order;

        if (exResponse.Message.Headers.TryGetValues("Location", out var orderData))
        {
          var orderItem = orderData.First();
          var orderId = $"{orderItem[(orderItem.LastIndexOf('/') + 1)..]}";

          if (string.IsNullOrEmpty(orderId))
          {
            inResponse.Errors.Add(new ErrorModel { ErrorMessage = $"{exResponse.Message.StatusCode}" });
            return inResponse;
          }

          inResponse.Data.Transaction.Id = orderId;
          inResponse.Data.Transaction.Status = Terminal.Core.Enums.OrderStatusEnum.Filled;
          Account.ActiveOrders.Enqueue(order);
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
        var exResponse = await SendData<OrderMessage>($"/trader/v1/accounts/{_accountCode}/orders/{order.Transaction.Id}", HttpMethod.Delete);

        if ((int)exResponse.Message.StatusCode >= 400)
        {
          inResponse.Errors.Add(new ErrorModel { ErrorMessage = $"{exResponse.Message.StatusCode}" });
          return inResponse;
        }

        inResponse.Data = order;
        inResponse.Data.Transaction.Status = Terminal.Core.Enums.OrderStatusEnum.Canceled;
      }
      catch (Exception e)
      {
        inResponse.Errors.Add(new ErrorModel { ErrorMessage = $"{e}" });
      }

      return inResponse;
    }
  }
}
