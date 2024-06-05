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
using System.Threading.Tasks;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Models;
using Terminal.Core.Services;
using Terminal.Gateway.Schwab.Messages;

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

      var response = await SendData<OptionChainMessage>($"/marketdata/v1/chains?{props.ToQuery()}", HttpMethod.Get, props);
      var options = response
        .Data
        .PutExpDateMap
        ?.Concat(response.Data.CallExpDateMap)
        ?.SelectMany(dateMap => dateMap.Value.SelectMany(o => o.Value))
        ?.Select(o =>
        {
          var option = new OptionModel
          {
            Name = o.Symbol,
            BaseName = response.Data.Symbol,
            OpenInterest = o.OpenInterest ?? 0,
            Strike = o.StrikePrice ?? 0,
            IntrinsicValue = o.IntrinsicValue ?? 0,
            Leverage = o.Multiplier ?? 0,
            Volatility = o.Volatility ?? 0,
            Volume = o.TotalVolume ?? 0,
            Point = new PointModel
            {
              Ask = o.Ask ?? 0,
              AskSize = o.AskSize ?? 0,
              Bid = o.Bid ?? 0,
              BidSize = o.BidSize ?? 0,
              Bar = new BarModel
              {
                Low = o.LowPrice ?? 0,
                High = o.LowPrice ?? 0,
                Open = o.OpenPrice ?? 0,
                Close = o.ClosePrice ?? 0
              }
            },
            Derivatives = new DerivativeModel
            {
              Rho = o.Rho ?? 0,
              Vega = o.Vega ?? 0,
              Delta = o.Delta ?? 0,
              Gamma = o.Gamma ?? 0,
              Theta = o.Theta ?? 0
            }
          };

          switch (o.PutCall.ToUpper())
          {
            case "PUT": option.Side = OptionSideEnum.Put; break;
            case "CALL": option.Side = OptionSideEnum.Call; break;
          }

          if (o.ExpirationDate is not null)
          {
            option.ExpirationDate = DateTimeOffset.FromUnixTimeMilliseconds(o.ExpirationDate.Value).UtcDateTime;
          }

          return option;

        })?.ToList() ?? [];

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
    /// <exception cref="NotImplementedException"></exception>
    public override Task<ResponseItemModel<IList<PointModel>>> GetPoints(PointMessageModel message)
    {
      var response = new ResponseItemModel<IList<PointModel>>
      {
        Data = Account.Instruments[message.Name].Points
      };

      return Task.FromResult(response);
    }

    /// <summary>
    /// Create orders
    /// </summary>
    /// <param name="orders"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
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
    protected async Task GetAccountData()
    {
      try
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

        var account = await SendData<AccountsMessage>($"/trader/v1/accounts/{_accountCode}", HttpMethod.Get, accountProps);
        var orders = await SendData<OrderMessage[]>($"/trader/v1/accounts/{_accountCode}/orders", HttpMethod.Get, orderProps);

        Account.Balance = account.Data.AggregatedBalance.CurrentLiquidationValue;
        Account.ActiveOrders = orders
          .Data
          .Where(o => o.CloseTime is null)
          .Select(GetInternalOrder)
          .ToDictionary(o => o.Transaction.Id, o => o);

        Account.ActivePositions = account
          .Data
          .SecuritiesAccount
          .Positions
          .Select(GetInternalPosition).ToDictionary(o => o.Order.Transaction.Id, o => o);

        Account.ActiveOrders.ForEach(async o => await Subscribe(o.Value.Transaction.Instrument.Name));
        Account.ActivePositions.ForEach(async o => await Subscribe(o.Value.Order.Transaction.Instrument.Name));
      }
      catch (Exception e)
      {
        InstanceService<MessageService>.Instance.OnMessage($"{e}");
      }
    }

    /// <summary>
    /// Convert remote position to local
    /// </summary>
    /// <param name="position"></param>
    /// <returns></returns>
    protected PositionModel GetInternalPosition(PositionMessage position)
    {
      var instrument = new Instrument
      {
        Name = position.Instrument.Symbol
      };

      var action = new TransactionModel
      {
        Instrument = instrument,
        Price = position.AveragePrice,
        Descriptor = position.Instrument.Symbol,
        Volume = position.LongQuantity + position.ShortQuantity,
        CurrentVolume = position.LongQuantity + position.ShortQuantity
      };

      var order = new OrderModel
      {
        Transaction = action,
        Type = OrderTypeEnum.Market,
        Side = GetInternalPositionSide(position)
      };

      var gainLossPoints = 0.0;
      var gainLoss = position.LongOpenProfitLoss;

      return new PositionModel
      {
        GainLossPointsMax = gainLossPoints,
        GainLossPointsMin = gainLossPoints,
        GainLossPoints = gainLossPoints,
        GainLossMax = gainLoss,
        GainLossMin = gainLoss,
        GainLoss = gainLoss,
        Order = order,
        Orders = [order]
      };
    }

    /// <summary>
    /// Convert remote position side to local
    /// </summary>
    /// <param name="position"></param>
    /// <returns></returns>
    protected OrderSideEnum GetInternalPositionSide(PositionMessage position)
    {
      switch (true)
      {
        case true when position.LongQuantity > 0: return OrderSideEnum.Buy;
        case true when position.ShortQuantity > 0: return OrderSideEnum.Sell;
      }

      return OrderSideEnum.None;
    }

    /// <summary>
    /// Convert remote order to local
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    protected OrderModel GetInternalOrder(OrderMessage order)
    {
      var assets = order
        ?.OrderLegCollection
        ?.Select(o => o?.Instrument?.Symbol);

      var instrument = new Instrument
      {
        Name = string.Join($" {Environment.NewLine}", assets)
      };

      var action = new TransactionModel
      {
        Id = $"{order.OrderId}",
        Descriptor = order.OrderId,
        Instrument = instrument,
        CurrentVolume = order.FilledQuantity,
        Volume = order.Quantity,
        Time = order.EnteredTime,
        Status = GetInternalStatus(order)
      };

      var inOrder = new OrderModel
      {
        Transaction = action,
        Type = OrderTypeEnum.Market,
        Side = GetInternalOrderSide(order),
        TimeSpan = GetInternalTimeSpan(order)
      };

      switch (order.OrderType.ToUpper())
      {
        case "STOP":
          inOrder.Type = OrderTypeEnum.Stop;
          inOrder.Price = order.Price;
          break;

        case "LIMIT":
          inOrder.Type = OrderTypeEnum.Limit;
          inOrder.Price = order.Price;
          break;

        case "STOP_LIMIT":
          inOrder.Type = OrderTypeEnum.StopLimit;
          inOrder.Price = order.Price;
          inOrder.ActivationPrice = order.StopPrice;
          break;
      }

      return inOrder;
    }

    /// <summary>
    /// Send data to the API
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="verb"></param>
    /// <param name="query"></param>
    /// <returns></returns>
    protected async Task<Distribution.Stream.Models.ResponseModel<T>> SendData<T>(
      string source,
      HttpMethod verb = null,
      Hashtable query = null,
      object content = null)
    {
      query ??= [];
      verb ??= HttpMethod.Get;

      var uri = new UriBuilder(DataUri)
      {
        Path = source
      };

      var message = new HttpRequestMessage
      {
        Method = verb
      };

      switch (true)
      {
        case true when Equals(verb, HttpMethod.Get): uri.Query = query.ToQuery(); break;
        case true when Equals(verb, HttpMethod.Put):
        case true when Equals(verb, HttpMethod.Post):
        case true when Equals(verb, HttpMethod.Patch): message.Content = new StringContent(JsonSerializer.Serialize(content)); break;
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
    protected async Task<ScopeMessage> UpdateToken<T>(string source)
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
    protected async Task<ResponseItemModel<OrderModel>> CreateOrder(OrderModel order)
    {
      var inResponse = new ResponseItemModel<OrderModel>();

      try
      {
        var exOrder = GetExternalOrder(order);
        var exResponse = await SendData<OrderMessage>($"/trader/v1/accounts/{_accountCode}/orders", HttpMethod.Post, null, exOrder);

        inResponse.Data = order;
        inResponse.Data.Transaction.Id = $"{exResponse.Data.OrderId}";
        inResponse.Data.Transaction.Status = GetInternalStatus(exResponse.Data);

        if (exResponse.Status < 400)
        {
          Account.ActiveOrders.Add(order.Transaction.Id, order);
        }
      }
      catch (Exception e)
      {
        inResponse.Errors.Add(new ErrorModel
        {
          ErrorMessage = e.Message
        });
      }

      return inResponse;
    }


    /// <summary>
    /// Convert remote order from brokerage to local record
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    protected OrderMessage GetExternalOrder(OrderModel order)
    {
      var action = order.Transaction;
      var message = new OrderMessage
      {
        //Quantity = action.Volume,
        //Symbol = action.Instrument.Name,
        //TimeInForce = GetExternalTimeSpan(order.TimeSpan.Value),
        //OrderType = "market"
      };

      //switch (order.Side)
      //{
      //  case OrderSideEnum.Buy: message.OrderSide = "buy"; break;
      //  case OrderSideEnum.Sell: message.OrderSide = "sell"; break;
      //}

      //switch (order.Type)
      //{
      //  case OrderTypeEnum.Stop: message.StopPrice = order.Price; break;
      //  case OrderTypeEnum.Limit: message.LimitPrice = order.Price; break;
      //  case OrderTypeEnum.StopLimit: message.StopPrice = order.ActivationPrice; message.LimitPrice = order.Price; break;
      //}

      //if (order.Orders.Any())
      //{
      //  message.OrderClass = "bracket";

      //  switch (order.Side)
      //  {
      //    case OrderSideEnum.Buy:
      //      message.StopLoss = GetExternalBracket(order, 1);
      //      message.TakeProfit = GetExternalBracket(order, -1);
      //      break;

      //    case OrderSideEnum.Sell:
      //      message.StopLoss = GetExternalBracket(order, -1);
      //      message.TakeProfit = GetExternalBracket(order, 1);
      //      break;
      //  }
      //}

      return null;
    }

    /// <summary>
    /// Cancel order
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    protected async Task<ResponseItemModel<OrderModel>> DeleteOrder(OrderModel order)
    {
      var inResponse = new ResponseItemModel<OrderModel>();

      try
      {
        var exResponse = await SendData<OrderMessage>($"/trader/v1/accounts/{_accountCode}/orders", HttpMethod.Delete);

        inResponse.Data = order;
        inResponse.Data.Transaction.Id = $"{exResponse.Data.OrderId}";
        inResponse.Data.Transaction.Status = GetInternalStatus(exResponse.Data);

        if (exResponse.Status < 400)
        {
          Account.ActiveOrders.Remove(order.Transaction.Id);
        }
      }
      catch (Exception e)
      {
        inResponse.Errors.Add(new ErrorModel
        {
          ErrorMessage = e.Message
        });
      }

      return inResponse;
    }

    /// <summary>
    /// Convert remote order status to local
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    protected OrderStatusEnum GetInternalStatus(OrderMessage order)
    {
      switch (order.Status.ToUpper())
      {
        case "FILLED":
        case "REPLACED": return OrderStatusEnum.Filled;
        case "WORKING": return OrderStatusEnum.Partitioned;
        case "REJECTED":
        case "CANCELED":
        case "EXPIRED": return OrderStatusEnum.Canceled;
        case "NEW":
        case "PENDING_RECALL":
        case "PENDING_CANCEL":
        case "PENDING_REPLACE":
        case "PENDING_ACTIVATION":
        case "PENDING_ACKNOWLEDGEMENT":
        case "AWAITING_CONDITION":
        case "AWAITING_PARENT_ORDER":
        case "AWAITING_RELEASE_TIME":
        case "AWAITING_MANUAL_REVIEW":
        case "AWAITING_STOP_CONDITION": return OrderStatusEnum.Pending;
      }

      return OrderStatusEnum.None;
    }

    /// <summary>
    /// Convert remote order side to local
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    protected OrderSideEnum GetInternalOrderSide(OrderMessage order)
    {
      static bool E(string x, string o) => string.Equals(x, o, StringComparison.OrdinalIgnoreCase);

      var position = order
        ?.OrderLegCollection
        ?.FirstOrDefault();

      if (E(position.OrderLegType, "EQUITY"))
      {
        switch (position.Instruction.ToUpper())
        {
          case "BUY":
          case "BUY_TO_OPEN":
          case "BUY_TO_CLOSE": return OrderSideEnum.Buy;

          case "SELL":
          case "SELL_TO_OPEN":
          case "SELL_TO_CLOSE": return OrderSideEnum.Sell;
        }
      }

      return OrderSideEnum.None;
    }

    /// <summary>
    /// Convert remote time in force to local
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    protected OrderTimeSpanEnum GetInternalTimeSpan(OrderMessage order)
    {
      var span = order.Duration;
      var session = order.Session;

      static bool E(string x, string o) => string.Equals(x, o, StringComparison.OrdinalIgnoreCase);

      switch (true)
      {
        case true when E(span, "DAY"): return OrderTimeSpanEnum.Day;
        case true when E(span, "FOK") || E(span, "FILL_OR_KILL"): return OrderTimeSpanEnum.Fok;
        case true when E(span, "GTC") || E(span, "GOOD_TILL_CANCEL"): return OrderTimeSpanEnum.Gtc;
        case true when E(span, "IOC") || E(span, "IMMEDIATE_OR_CANCEL"): return OrderTimeSpanEnum.Ioc;
        case true when E(session, "AM"): return OrderTimeSpanEnum.Am;
        case true when E(session, "PM"): return OrderTimeSpanEnum.Pm;
      }

      return OrderTimeSpanEnum.None;
    }
  }
}
