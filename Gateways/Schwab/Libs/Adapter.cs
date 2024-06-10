using Distribution.Stream;
using Distribution.Stream.Extensions;
using Schwab.Mappers;
using Schwab.Messages;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Terminal.Core.Domains;
using Terminal.Core.Models;

namespace Schwab
{
  public class Adapter : Gateway
  {
    /// <summary>
    /// Encrypted account number
    /// </summary>
    protected string _accountCode;

    /// <summary>
    /// Tokens
    /// </summary>
    protected ScopeMessage _scope;

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
    /// Data endpoint
    /// </summary>
    public virtual string DataUri { get; set; }

    /// <summary>
    /// Streaming endpoint
    /// </summary>
    public virtual string StreamUri { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public Adapter()
    {
      DataUri = "https://api.schwabapi.com";

      _subscriptions = [];
      _connections = [];
    }

    /// <summary>
    /// Connect
    /// </summary>
    public override async Task<IList<ErrorModel>> Connect()
    {
      await Disconnect();

      _sender = new Service();

      await GetAccountData();

      _connections.Add(_sender);
      _connections.Add(_streamer);

      return null;
    }

    /// <summary>
    /// Subscribe to data streams
    /// </summary>
    public override async Task<IList<ErrorModel>> Subscribe(string name)
    {
      await Unsubscribe(name);

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
    public override Task<IList<ErrorModel>> Unsubscribe(string name)
    {
      _subscriptions?.ForEach(o => o.Dispose());
      _subscriptions?.Clear();

      return Task.FromResult<IList<ErrorModel>>(null);
    }

    /// <summary>
    /// Get option chains
    /// </summary>
    /// <param name="message"></param>
    public override async Task<ResponseItemModel<IList<OptionModel>>> GetOptions(OptionMessageModel message)
    {
      var props = new Hashtable
      {
        ["symbol"] = message.Name,
        ["fromDate"] = $"{message.MinDate:yyyy-MM-dd}",
        ["toDate"] = $"{message.MaxDate:yyyy-MM-dd}",
        ["includeQuotes"] = "TRUE"
      };

      var response = await SendData<OptionChainMessage>($"/marketdata/v1/chains?{props.ToQuery()}");
      var options = response
        .Data
        .PutExpDateMap
        ?.Concat(response.Data.CallExpDateMap)
        ?.SelectMany(dateMap => dateMap.Value.SelectMany(o => o.Value))
        ?.Select(InternalMap.GetOption)?.ToList() ?? [];

      return new ResponseItemModel<IList<OptionModel>>
      {
        Data = options
      };
    }

    /// <summary>
    /// Get points
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public override async Task<ResponseItemModel<IDictionary<string, PointModel>>> GetPoint(PointMessageModel message)
    {
      var props = new Hashtable
      {
        ["symbols"] = string.Join(",", message.Names),
        ["fields"] = "quote,fundamental,extended,reference,regular"
      };

      var pointResponse = await SendData<Dictionary<string, AssetMessage>>($"/marketdata/v1/quotes?{props.ToQuery()}");
      var response = new ResponseItemModel<IDictionary<string, PointModel>>
      {
        Data = message
          .Names
          .Select(name => InternalMap.GetPoint(pointResponse.Data[name]))
          .ToDictionary(o => o.Instrument.Name)
      };

      return response;
    }

    /// <summary>
    /// Create orders
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
      var dateFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
      var orderProps = new Hashtable
      {
        ["maxResults"] = 50,
        ["toEnteredTime"] = DateTime.Now.AddDays(5).ToString(dateFormat),
        ["fromEnteredTime"] = DateTime.Now.AddDays(-100).ToString(dateFormat)
      };

      var accountProps = new Hashtable { ["fields"] = "positions" };
      var accountNumbers = await SendData<AccountNumberMessage[]>("/trader/v1/accounts/accountNumbers");

      _accountCode = accountNumbers.Data.First(o => Equals(o.AccountNumber, Account.Descriptor)).HashValue;

      var account = await SendData<AccountsMessage>($"/trader/v1/accounts/{_accountCode}?{accountProps.ToQuery()}");
      var orders = await SendData<OrderMessage[]>($"/trader/v1/accounts/{_accountCode}/orders?{orderProps.ToQuery()}");

      Account.Balance = account.Data.AggregatedBalance.CurrentLiquidationValue;
      Account.ActiveOrders = orders
        .Data
        .Where(o => o.CloseTime is null)
        .Select(InternalMap.GetOrder)
        .ToDictionary(o => o.Transaction.Id, o => o);

      Account.ActivePositions = account
        .Data
        .SecuritiesAccount
        .Positions
        .Select(InternalMap.GetPosition)
        .ToDictionary(o => o.Order.Transaction.Id, o => o);

      Account.ActiveOrders.ForEach(async o => await Subscribe(o.Value.Transaction.Instrument.Name));
      Account.ActivePositions.ForEach(async o => await Subscribe(o.Value.Order.Transaction.Instrument.Name));
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
      verb ??= HttpMethod.Get;

      var uri = new UriBuilder(DataUri + source);
      var message = new HttpRequestMessage
      {
        Method = verb
      };

      switch (true)
      {
        case true when Equals(verb, HttpMethod.Put):
        case true when Equals(verb, HttpMethod.Post):
        case true when Equals(verb, HttpMethod.Patch):
          message.Content = new StringContent(JsonSerializer.Serialize(content), new MediaTypeHeaderValue("application/json"));
          break;
      }

      message.RequestUri = uri.Uri;
      message.Headers.Add("Authorization", $"Bearer {_scope.AccessToken}");

      return await _sender.Send<T>(message, _sender.Options);
    }
    /// <summary>
    /// Refresh token
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <returns></returns>
    protected virtual async Task<ScopeMessage> UpdateToken<T>(string source)
    {
      var uri = new UriBuilder(DataUri)
      {
        Path = source
      };

      var content = new FormUrlEncodedContent(new Dictionary<string, string>
      {
        ["grant_type"] = "refresh_token",
        ["refresh_token"] = _scope.RefreshToken
      });

      var message = new HttpRequestMessage();
      var basicToken = Encoding.UTF8.GetBytes($"{ConsumerKey}:{ConsumerSecret}");

      message.Content = content;
      message.RequestUri = uri.Uri;
      message.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
      message.Headers.Add("Authorization", $"Basic {Convert.ToBase64String(basicToken)}");

      var response = await _sender.Send<ScopeMessage>(message, _sender.Options);

      return _scope = response.Data;
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
        var exResponse = await SendData<OrderMessage>($"/trader/v1/accounts/{_accountCode}/orders", HttpMethod.Post, exOrder);

        inResponse.Data = order;

        if (exResponse.Message.Headers.TryGetValues("Location", out var orderData))
        {
          var orderItem = orderData.First();
          var orderId = $"{orderItem.Substring(orderItem.LastIndexOf('/') + 1)}";

          if (string.IsNullOrEmpty(orderId))
          {
            inResponse.Errors.Add(new ErrorModel { ErrorMessage = $"{exResponse.Message.StatusCode}" });
            return inResponse;
          }

          inResponse.Data.Transaction.Id = orderId;
          inResponse.Data.Transaction.Status = Terminal.Core.Enums.OrderStatusEnum.Filled;
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
        var exResponse = await SendData<OrderMessage>($"/trader/v1/accounts/{_accountCode}/orders/{order.Transaction.Id}", HttpMethod.Delete);

        if ((int)exResponse.Message.StatusCode >= 400)
        {
          inResponse.Errors.Add(new ErrorModel { ErrorMessage = $"{exResponse.Message.StatusCode}" });
          return inResponse;
        }

        inResponse.Data = order;
        inResponse.Data.Transaction.Status = Terminal.Core.Enums.OrderStatusEnum.Closed;
      }
      catch (Exception e)
      {
        inResponse.Errors.Add(new ErrorModel { ErrorMessage = $"{e}" });
      }

      return inResponse;
    }
  }
}
