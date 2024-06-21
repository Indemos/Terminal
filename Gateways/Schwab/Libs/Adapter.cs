using Distribution.Services;
using Distribution.Stream;
using Distribution.Stream.Extensions;
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
using System.Threading.Tasks;
using System.Timers;
using Terminal.Core.Domains;
using Terminal.Core.Models;
using Terminal.Core.Services;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
    /// Disposable subscriptions
    /// </summary>
    protected IList<IDisposable> _subscriptions;

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
        await Disconnect();

        _sender = new Service();

        await GetAccountData();

        var interval = new Timer(TimeSpan.FromMinutes(1));

        await UpdateToken("/v1/oauth/token");

        interval.Enabled = true;
        interval.Elapsed += async (sender, e) => await UpdateToken("/v1/oauth/token");

        _connections.Add(_sender);
        _connections.Add(_streamer);
        _connections.Add(interval);
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
    public override Task<IList<ErrorModel>> Unsubscribe(string name)
    {
      var errors = new List<ErrorModel>();

      try
      {
        _subscriptions?.ForEach(o => o.Dispose());
        _subscriptions?.Clear();
      }
      catch (Exception e)
      {
        errors.Add(new ErrorModel { ErrorMessage = $"{e}" });
      }

      return Task.FromResult<IList<ErrorModel>>(errors);
    }

    /// <summary>
    /// Get options
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseItemModel<IList<OptionModel>>> GetOptions(Hashtable criteria)
    {
      var response = new ResponseItemModel<IList<OptionModel>>();

      try
      {
        var optionResponse = await SendData<OptionChainMessage>($"/marketdata/v1/chains?{criteria.ToQuery()}");

        response.Data = optionResponse
          .Data
          .PutExpDateMap
          ?.Concat(optionResponse.Data.CallExpDateMap)
          ?.SelectMany(dateMap => dateMap.Value.SelectMany(o => o.Value))
          ?.Select(InternalMap.GetOption)?.ToList() ?? [];

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
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseItemModel<IDictionary<string, PointModel>>> GetPoint(Hashtable criteria)
    {
      var response = new ResponseItemModel<IDictionary<string, PointModel>>();

      try
      {
        var names = $"{criteria["symbols"]}".Split(",");
        var pointResponse = await SendData<Dictionary<string, AssetMessage>>($"/marketdata/v1/quotes?{criteria.ToQuery()}");

        response.Data = names
          .Select(name => InternalMap.GetPoint(pointResponse.Data[name]))
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
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override async Task<ResponseItemModel<IList<PointModel>>> GetPoints(Hashtable criteria)
    {
      var response = new ResponseItemModel<IList<PointModel>>();

      try
      {
        var pointResponse = await SendData<BarsMessage>($"/marketdata/v1/pricehistory?{criteria.ToQuery()}");

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
