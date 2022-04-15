using Core.CollectionSpace;
using Core.EnumSpace;
using Core.ManagerSpace;
using Core.ModelSpace;
using Gateway.Oanda.ModelSpace;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Gateway.Oanda
{
  /// <summary>
  /// Implementation
  /// </summary>
  public class GatewayClient : GatewayModel, IGatewayModel
  {
    /// <summary>
    /// API key
    /// </summary>
    public string Token { get; set; }

    /// <summary>
    /// HTTP endpoint
    /// </summary>
    public string Source { get; set; } = "https://api-fxpractice.oanda.com";

    /// <summary>
    /// Socket endpoint
    /// </summary>
    public string StreamSource { get; set; } = "https://stream-fxpractice.oanda.com";

    /// <summary>
    /// Establish connection with a server
    /// </summary>
    /// <param name="docHeader"></param>
    public override Task Connect()
    {
      return Task.Run(async () =>
      {
        try
        {
          await Disconnect();

          _connections.Add(_serviceClient ??= new ClientService());

          await GetOrders();
          await GetPositions();
          await GetActiveOrders();
          await GetActivePositions();
          await GetAccountProps();
          await Subscribe();
        }
        catch (Exception e)
        {
          InstanceManager<LogService>.Instance.Log.Error(e.ToString());
        }
      });
    }

    /// <summary>
    /// Disconnect
    /// </summary>
    /// <returns></returns>
    public override Task Disconnect()
    {
      Unsubscribe();

      _connections.ForEach(o => o.Dispose());
      _connections.Clear();

      return Task.FromResult(0);
    }

    /// <summary>
    /// Start streaming
    /// </summary>
    /// <returns></returns>
    public override async Task Subscribe()
    {
      await Unsubscribe();

      // Streaming

      var stream = await GetStream();
      var span = TimeSpan.FromMilliseconds(0);
      var scheduler = InstanceManager<ScheduleService>.Instance.Scheduler;
      var reader = new StreamReader(stream);
      var process = Observable
        .Interval(span, scheduler)
        .Take(1)
        .Subscribe(async o =>
        {
          while (_subscriptions.Any())
          {
            var message = await reader.ReadLineAsync();

            if (message.IndexOf("HEARTBEAT", StringComparison.OrdinalIgnoreCase) < 0)
            {
              OnInputData(ConversionManager.Deserialize<InputPointModel>(message));
            }
          }
        });

      _subscriptions.Add(reader);
      _subscriptions.Add(stream);

      // Orders

      var orderSubscription = OrderSenderStream.Subscribe(message =>
      {
        switch (message.Action)
        {
          case ActionEnum.Create: CreateOrders(message.Next); break;
          case ActionEnum.Update: UpdateOrders(message.Next); break;
          case ActionEnum.Delete: DeleteOrders(message.Next); break;
        }
      });

      _subscriptions.Add(orderSubscription);
    }

    public override Task Unsubscribe()
    {
      _subscriptions.ForEach(o => o.Dispose());
      _subscriptions.Clear();

      return Task.FromResult(0);
    }

    public override Task<IEnumerable<ITransactionOrderModel>> CreateOrders(params ITransactionOrderModel[] orders)
    {
      return Task.FromResult(orders as IEnumerable<ITransactionOrderModel>);
    }

    public override Task<IEnumerable<ITransactionOrderModel>> UpdateOrders(params ITransactionOrderModel[] orders)
    {
      return Task.FromResult(orders as IEnumerable<ITransactionOrderModel>);
    }

    public override Task<IEnumerable<ITransactionOrderModel>> DeleteOrders(params ITransactionOrderModel[] orders)
    {
      return Task.FromResult(orders as IEnumerable<ITransactionOrderModel>);
    }

    /// <summary>
    /// Process incoming quotes
    /// </summary>
    /// <param name="input"></param>
    protected void OnInputData(InputPointModel input)
    {
      var currentAsk = input.Ask;
      var currentBid = input.Bid;
      var previousAsk = _point?.Ask ?? currentAsk;
      var previousBid = _point?.Bid ?? currentBid;
      var instrument = $"{ input.Instrument }";

      var point = new PointModel
      {
        Ask = currentAsk,
        Bid = currentBid,
        Time = input.Time,
        Bar = new PointBarModel(),
        Instrument = Account.Instruments[instrument],
        Last = ConversionManager.Compare(currentBid, previousBid) ? currentAsk : currentBid
      };

      if (input.Asks.Any())
      {
        var edge = input.Asks.Min(o => new { o.Price, o.Size });

        point.Ask = Math.Min(point.Ask.Value, edge.Price.Value);
        point.AskSize = edge.Size;
      }

      if (input.Bids.Any())
      {
        var edge = input.Bids.Max(o => new { o.Price, o.Size });

        point.Bid = Math.Max(point.Bid.Value, edge.Price.Value);
        point.BidSize = edge.Size;
      }

      _point = point;

      UpdatePointProps(point);
    }

    /// <summary>
    /// Process incoming quotes
    /// </summary>
    /// <param name="input"></param>
    protected void OnInputTrade(dynamic input)
    {
    }

    /// <summary>
    /// Get account properties
    /// </summary>
    protected async Task GetAccountProps()
    {
      var inputAccount = (await GetResponse<InputAccountItemModel>($"/v3/accounts/{ Account.Id }")).Account;

      Account.Currency = inputAccount.Currency.ToUpper();
      Account.Balance = ConversionManager.To<double>(inputAccount.Balance);
      Account.Leverage = inputAccount.MarginRate == 0 ? 1 : 1 / inputAccount.MarginRate;
    }

    /// <summary>
    /// Get past orders
    /// </summary>
    protected async Task GetOrders()
    {
      var inputs = new
      {
        state = "ALL",
        count = 500
      };

      var orders = await GetGenericOrders(inputs);

      foreach (var order in orders)
      {
        if (Equals(order.Status, OrderStatusEnum.Placed) == false)
        {
          Account.Orders.Add(order);
        }
      }
    }

    /// <summary>
    /// Get pending orders
    /// </summary>
    protected async Task GetActiveOrders()
    {
      var inputs = new
      {
        state = "PENDING",
        count = 500
      };

      Account.ActiveOrders = await GetGenericOrders(inputs);
    }

    /// <summary>
    /// Get orders by criteria
    /// </summary>
    /// <param name="inputs"></param>
    /// <returns></returns>
    protected async Task<IndexCollection<ITransactionOrderModel>> GetGenericOrders(dynamic inputs)
    {
      var orders = new IndexCollection<ITransactionOrderModel>();
      var response = await GetResponse<InputOrderListModel>($"/v3/accounts/{ Account.Id }/orders");

      foreach (var inputOrder in response.Orders)
      {
        var orderModel = new TransactionOrderModel
        {
          Id = $"{ inputOrder.Id }",
          Size = inputOrder.Size,
          Price = inputOrder.Price,
          Time = inputOrder.CreationTime,
          Type = OrderTypeMap.Input(inputOrder.Type),
          Status = OrderStatusMap.Input(inputOrder.Status),
          TimeSpan = OrderTimeSpanMap.Input(inputOrder.TimeSpan),
          DealTime = inputOrder.FillTime ?? inputOrder.CancellationTime ?? inputOrder.TriggerTime
        };

        orders.Add(orderModel);
      }

      return orders;
    }

    /// <summary>
    /// Get past deals
    /// </summary>
    protected async Task GetPositions()
    {
      var inputs = new
      {
        state = "ALL",
        count = 500
      };

      var positions = await GetGenericPositions(inputs);

      foreach (var position in positions)
      {
        if (Equals(position.Status, OrderStatusEnum.Filled) == false)
        {
          Account.Positions.Add(position);
        }
      }
    }

    /// <summary>
    /// Get active positions
    /// </summary>
    protected async Task GetActivePositions()
    {
      var inputs = new
      {
        state = "OPEN",
        count = 500
      };

      Account.ActivePositions = await GetGenericPositions(inputs);
    }

    /// <summary>
    /// Get positions by criteria
    /// </summary>
    /// <param name="inputs"></param>
    /// <returns></returns>
    protected async Task<IndexCollection<ITransactionPositionModel>> GetGenericPositions(dynamic inputs)
    {
      var positions = new IndexCollection<ITransactionPositionModel>();
      var response = await GetResponse<InputDealListModel>($"/v3/accounts/{ Account.Id }/trades");

      foreach (var inputOrder in response.Deals)
      {
        var positionModel = new TransactionPositionModel
        {
          Id = $"{ inputOrder.Id }",
          Size = inputOrder.Size,
          Price = inputOrder.Price,
          OpenPrice = inputOrder.Price,
          Type = OrderTypeMap.Input(inputOrder.Type),
          Status = DealStatusMap.Input(inputOrder.Status),
          TimeSpan = OrderTimeSpanMap.Input(inputOrder.TimeSpan),
          Time = inputOrder.OpenTime ?? inputOrder.CreationTime,
          DealTime = inputOrder.OpenTime
        };

        positions.Add(positionModel);
      }

      return positions;
    }

    /// <summary>
    /// Create stream
    /// </summary>
    /// <returns></returns>
    protected async Task<Stream> GetStream()
    {
      var query = new Dictionary<dynamic, dynamic>
      {
        ["instruments"] = string.Join(",", Account.Instruments.Select(o => o.Value.Name))
      };

      var headers = new Dictionary<dynamic, dynamic>
      {
        ["Authorization"] = $"Bearer { Token }"
      };

      return await _serviceClient.Stream($"{ StreamSource }/v3/accounts/{ Account.Id }/pricing/stream", query, headers);
    }

    /// <summary>
    /// Send HTTP query
    /// </summary>
    /// <param name="source"></param>
    /// <param name="inputs"></param>
    /// <param name="variables"></param>
    /// <returns></returns>
    protected async Task<T> GetResponse<T>(
      string endpoint,
      IDictionary<dynamic, dynamic> inputs = null,
      IDictionary<dynamic, dynamic> variables = null)
    {
      var headers = new Dictionary<dynamic, dynamic>
      {
        ["Authorization"] = $"Bearer { Token }"
      };

      return variables == null ? 
        await _serviceClient.Get<T>(Source + endpoint + "?" + ConversionManager.GetQuery(inputs), null, headers) : 
        await _serviceClient.Post<T>(Source + endpoint + "?" + ConversionManager.GetQuery(inputs), variables, headers);
    }
  }
}
