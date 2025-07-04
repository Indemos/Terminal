using Distribution.Services;
using Distribution.Stream;
using MessagePack;
using MessagePack.Resolvers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Extensions;
using Terminal.Core.Models;

namespace Simulation
{
  public class Adapter : Gateway, IDisposable
  {
    /// <summary>
    /// HTTP service
    /// </summary>
    protected Service sender;

    /// <summary>
    /// Disposable connections
    /// </summary>
    protected IList<IDisposable> connections;

    /// <summary>
    /// Subscription states
    /// </summary>
    protected ConcurrentDictionary<string, SummaryModel> states;

    /// <summary>
    /// Disposable subscriptions
    /// </summary>
    protected ConcurrentDictionary<string, Action> subscriptions;

    /// <summary>
    /// Instrument streams
    /// </summary>
    protected ConcurrentDictionary<string, IEnumerator<string>> streams;

    /// <summary>
    /// Message pack options
    /// </summary>
    protected MessagePackSerializerOptions messageOptions;

    /// <summary>
    /// Simulation speed in milliseconds
    /// </summary>
    public virtual int Speed { get; set; }

    /// <summary>
    /// Location of the files with quotes
    /// </summary>
    public virtual string Source { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public Adapter()
    {
      Speed = 1000;

      states = new();
      streams = new();
      subscriptions = new();
      connections = [];
      sender = new Service();
      messageOptions = MessagePackSerializerOptions.Standard.WithResolver(ContractlessStandardResolver.Instance);
    }

    /// <summary>
    /// Connect
    /// </summary>
    public override async Task<ResponseModel<StatusEnum>> Connect()
    {
      var response = new ResponseModel<StatusEnum>();

      await Disconnect();

      SetupAccounts(Account);

      streams = Account
        .States
        .ToDictionary(
          o => o.Key,
          o => Directory
            .EnumerateFiles(Path.Combine(Source, o.Value.Instrument.Name), "*", SearchOption.AllDirectories)
            .GetEnumerator())
            .Concurrent();

      streams.ForEach(o => connections.Add(o.Value));

      await Task.WhenAll(Account.States.Values.Select(o => Subscribe(o.Instrument)));

      var span = TimeSpan.FromMicroseconds(Speed);
      var scheduler = InstanceService<ScheduleService>.Instance;
      var interval = new Timer(span);

      interval.Enabled = true;
      interval.AutoReset = true;
      interval.Elapsed += (sender, e) => scheduler.Send(() => subscriptions.Values.ForEach(o => o()));

      connections.Add(interval);

      Stream += OnPoint;

      response.Data = StatusEnum.Active;

      return response;
    }

    /// <summary>
    /// Subscribe to streams
    /// </summary>
    /// <param name="instrument"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<StatusEnum>> Subscribe(InstrumentModel instrument)
    {
      var response = new ResponseModel<StatusEnum>();

      await Unsubscribe(instrument);

      Account.States.Get(instrument.Name).Instrument ??= instrument;

      subscriptions[instrument.Name] = async () =>
      {
        states.TryGetValue(instrument.Name, out var state);
        streams.TryGetValue(instrument.Name, out var stream);

        if (stream is not null && (state is null || state.Status is StatusEnum.Pause))
        {
          stream.MoveNext();

          switch (string.IsNullOrEmpty(stream.Current))
          {
            case true:
              states[instrument.Name].Status = StatusEnum.Inactive;
              break;

            case false:
              states[instrument.Name] = await GetState(instrument.Name, stream.Current);
              states[instrument.Name].Status = StatusEnum.Active;
              break;
          }
        }

        var next = states.First();

        states.ForEach(o => next = o.Value.Instrument.Point.Time <= next.Value.Instrument.Point.Time ? o : next);

        if (Equals(next.Key, instrument.Name))
        {
          var summary = Account.States.Get(instrument.Name);

          summary.Instrument = instrument;
          summary.Instrument.Point = next.Value.Instrument.Point;
          summary.Instrument.Point.Bar = null;
          summary.Instrument.Point.Name = instrument.Name;
          summary.Instrument.Point.TimeFrame = summary.TimeFrame;
          summary.Instrument.Point.Account = Account;
          summary.Dom = next.Value.Dom;
          summary.Options = next.Value.Options;
          summary.Points.Add(summary.Instrument.Point);
          summary.PointGroups.Add(summary.Instrument.Point, summary.TimeFrame);

          Stream(new MessageModel<PointModel> { Next = summary.PointGroups.Last() });

          states[instrument.Name].Status = StatusEnum.Pause;
        }
      };

      response.Data = StatusEnum.Active;

      return response;
    }

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public override async Task<ResponseModel<StatusEnum>> Disconnect()
    {
      var response = new ResponseModel<StatusEnum>();

      Stream -= OnPoint;

      await Task.WhenAll(Account.States.Values.Select(o => Unsubscribe(o.Instrument)));

      connections?.ForEach(o => o?.Dispose());
      connections?.Clear();

      return response;
    }

    /// <summary>
    /// Unsubscribe from streams
    /// </summary>
    /// <param name="instrument"></param>
    /// <returns></returns>
    public override Task<ResponseModel<StatusEnum>> Unsubscribe(InstrumentModel instrument)
    {
      var response = new ResponseModel<StatusEnum>();

      subscriptions.TryRemove(instrument.Name, out var subscription);

      return Task.FromResult(response);
    }

    /// <summary>
    /// Create order and depending on the account, send it to the processing queue
    /// </summary>
    /// <param name="order"></param>
    public override async Task<ResponseModel<OrderModel>> SendOrder(OrderModel order)
    {
      var response = new ResponseModel<OrderModel>();

      if ((response.Errors = await SubscribeToOrder(order)).Count is 0)
      {
        foreach (var nextOrder in ComposeOrders(order))
        {
          switch (nextOrder.Type)
          {
            case OrderTypeEnum.Stop:
            case OrderTypeEnum.Limit:
            case OrderTypeEnum.StopLimit: SendPendingOrder(nextOrder); break;
            case OrderTypeEnum.Market: ProcessOrder(nextOrder); break;
          }

          nextOrder.Orders.ForEach(o => SendPendingOrder(o));
          response.Data = nextOrder;
        }
      }

      return response;
    }

    /// <summary>
    /// Recursively cancel orders
    /// </summary>
    /// <param name="orders"></param>
    public override Task<ResponseModel<List<OrderModel>>> ClearOrders(params OrderModel[] orders)
    {
      var response = new ResponseModel<List<OrderModel>>
      {
        Data = [.. orders.Select(order =>
        {
          if (Account.Orders.TryGetValue(order.Id, out var o))
          {
            Account.Orders.Remove(order.Id, out var item);
          }

          return o;
        })]
      };

      return Task.FromResult(response);
    }

    /// <summary>
    /// Process pending order
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    protected virtual OrderModel SendPendingOrder(OrderModel order)
    {
      var nextOrder = order.Clone() as OrderModel;

      nextOrder.Id = order.Id;
      nextOrder.Status = OrderStatusEnum.Pending;

      Account.Orders[nextOrder.Id] = nextOrder;

      return order;
    }

    /// <summary>
    /// Position opening logic 
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    protected virtual OrderModel ProcessOrder(OrderModel order)
    {
      var nextOrder = order.Clone() as OrderModel;

      nextOrder.OpenAmount = nextOrder.Amount;
      nextOrder.Status = OrderStatusEnum.Filled;

      if (Account.Positions.TryGetValue(nextOrder.Name, out var currentOrder))
      {
        if (Equals(currentOrder.Side, nextOrder.Side))
        {
          return Account.Positions[nextOrder.Name] = IncreaseSide(currentOrder, nextOrder);
        }

        currentOrder = CloseSide(currentOrder, nextOrder);

        Account.Deals.Add(currentOrder);
        Account.Balance += currentOrder.GetValueEstimate();

        if ((currentOrder.OpenAmount - nextOrder.Amount).Is(0))
        {
          Account.Positions.Remove(nextOrder.Name, out var o);
          return currentOrder;
        }

        nextOrder = currentOrder.OpenAmount > nextOrder.Amount ?
          ReduceSide(currentOrder, nextOrder) :
          ReverseSide(currentOrder, nextOrder);
      }

      return Account.Positions[nextOrder.Name] = nextOrder;
    }

    /// <summary>
    /// Compute aggregated position price
    /// </summary>
    /// <param name="orders"></param>
    /// <returns></returns>
    protected virtual double? GetGroupPrice(params OrderModel[] orders)
    {
      var numerator = orders.Sum(o => o.OpenAmount * o.OpenPrice);
      var denominator = orders.Sum(o => o.OpenAmount);

      return numerator / denominator;
    }

    /// <summary>
    /// Increase size of the order
    /// </summary>
    /// <param name="currentOrder"></param>
    /// <param name="nextOrder"></param>
    /// <returns></returns>
    protected virtual OrderModel CloseSide(OrderModel currentOrder, OrderModel nextOrder)
    {
      currentOrder.Price = nextOrder.OpenPrice;
      return currentOrder;
    }

    /// <summary>
    /// Increase size of the order
    /// </summary>
    /// <param name="currentOrder"></param>
    /// <param name="nextOrder"></param>
    /// <returns></returns>
    protected virtual OrderModel IncreaseSide(OrderModel currentOrder, OrderModel nextOrder)
    {
      nextOrder.OpenPrice = GetGroupPrice(currentOrder, nextOrder);
      nextOrder.OpenAmount = currentOrder.OpenAmount + nextOrder.Amount;
      nextOrder.Amount = nextOrder.OpenAmount;
      return nextOrder;
    }

    /// <summary>
    /// Decrease size of the order
    /// </summary>
    /// <param name="currentOrder"></param>
    /// <param name="nextOrder"></param>
    /// <returns></returns>
    protected virtual OrderModel ReduceSide(OrderModel currentOrder, OrderModel nextOrder)
    {
      nextOrder.Side = currentOrder.Side;
      nextOrder.OpenPrice = currentOrder.OpenPrice;
      nextOrder.OpenAmount = currentOrder.OpenAmount - nextOrder.Amount;
      nextOrder.Amount = nextOrder.OpenAmount;
      return nextOrder;
    }

    /// <summary>
    /// Open opposite order
    /// </summary>
    /// <param name="currentOrder"></param>
    /// <param name="nextOrder"></param>
    /// <returns></returns>
    protected virtual OrderModel ReverseSide(OrderModel currentOrder, OrderModel nextOrder)
    {
      nextOrder.OpenAmount = nextOrder.Amount - currentOrder.OpenAmount;
      nextOrder.Amount = nextOrder.OpenAmount;
      return nextOrder;
    }

    /// <summary>
    /// Process pending orders on each quote
    /// </summary>
    /// <param name="message"></param>
    protected virtual void OnPoint(MessageModel<PointModel> message)
    {
      var estimates = Account
        .Positions
        .Select(o => o.Value.GetValueEstimate())
        .ToList();

      foreach (var order in Account.Orders.Values)
      {
        if (IsOrderExecutable(order))
        {
          ProcessOrder(order);

          Account.Orders = Account
            .Orders
            .Where(o => Equals(o.Value.Descriptor, order.Descriptor) is false)
            .ToDictionary(o => o.Key, o => o.Value)
            .Concurrent();
        }
      }
    }

    /// <summary>
    /// Check if pending order can be executed
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    protected virtual bool IsOrderExecutable(OrderModel order)
    {
      var isExecutable = false;
      var point = order.Instrument.Point;
      var isBuyStopLimit = order.Side is OrderSideEnum.Long && order.Type is OrderTypeEnum.StopLimit && point.Ask >= order.ActivationPrice;
      var isSellStopLimit = order.Side is OrderSideEnum.Short && order.Type is OrderTypeEnum.StopLimit && point.Bid <= order.ActivationPrice;

      order.Type = isBuyStopLimit || isSellStopLimit ? OrderTypeEnum.Limit : order.Type;

      var isBuyStop = order.Side is OrderSideEnum.Long && order.Type is OrderTypeEnum.Stop;
      var isSellStop = order.Side is OrderSideEnum.Short && order.Type is OrderTypeEnum.Stop;
      var isBuyLimit = order.Side is OrderSideEnum.Long && order.Type is OrderTypeEnum.Limit;
      var isSellLimit = order.Side is OrderSideEnum.Short && order.Type is OrderTypeEnum.Limit;

      isExecutable = isBuyStop || isSellLimit ? point.Ask >= order.OpenPrice : isExecutable;
      isExecutable = isSellStop || isBuyLimit ? point.Bid <= order.OpenPrice : isExecutable;

      return isExecutable;
    }

    /// <summary>
    /// Parse snapshot document to get current symbol and options state
    /// </summary>
    /// <param name="name"></param>
    /// <param name="source"></param>
    /// <returns></returns>
    protected virtual async Task<SummaryModel> GetState(string name, string source)
    {
      var document = new FileInfo(source);

      if (string.Equals(document.Extension, ".bin", StringComparison.InvariantCultureIgnoreCase))
      {
        var content = File.ReadAllBytes(source);

        return MessagePackSerializer.Deserialize<SummaryModel>(content, messageOptions);
      }

      if (string.Equals(document.Extension, ".zip", StringComparison.InvariantCultureIgnoreCase))
      {
        using (var stream = File.OpenRead(source))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
        using (var content = archive.Entries.First().Open())
        {
          return await JsonSerializer.DeserializeAsync<SummaryModel>(content, sender.Options);
        }
      }

      var inputMessage = File.ReadAllText(source);

      return JsonSerializer.Deserialize<SummaryModel>(inputMessage, sender.Options);
    }

    /// <summary>
    /// Get depth of market when available or just a top of the book
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override Task<ResponseModel<DomModel>> GetDom(ConditionModel criteria = null)
    {
      var response = new ResponseModel<DomModel>
      {
        Data = Account.States.Get(criteria.Instrument.Name).Dom
      };

      return Task.FromResult(response);
    }

    /// <summary>
    /// List of points by criteria, e.g. for specified instrument
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override Task<ResponseModel<List<PointModel>>> GetPoints(ConditionModel criteria = null)
    {
      var response = new ResponseModel<List<PointModel>>
      {
        Data = [.. Account.States.Get(criteria.Instrument.Name).Points]
      };

      return Task.FromResult(response);
    }

    /// <summary>
    /// Option chain
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override Task<ResponseModel<List<InstrumentModel>>> GetOptions(ConditionModel criteria = null)
    {
      var side = criteria
        ?.Instrument
        ?.Derivative
        ?.Side;

      var options = Account
        .States
        .Get(criteria.Instrument.Name)
        .Options
        .Where(o => side is null || Equals(o.Derivative.Side, side))
        .Where(o => criteria?.MinDate is null || o.Derivative.ExpirationDate?.Date >= criteria.MinDate?.Date)
        .Where(o => criteria?.MaxDate is null || o.Derivative.ExpirationDate?.Date <= criteria.MaxDate?.Date)
        .Where(o => criteria?.MinPrice is null || o.Derivative.Strike >= criteria.MinPrice)
        .Where(o => criteria?.MaxPrice is null || o.Derivative.Strike <= criteria.MaxPrice)
        .OrderBy(o => o.Derivative.ExpirationDate)
        .ThenBy(o => o.Derivative.Strike)
        .ThenBy(o => o.Derivative.Side)
        .Select(o => Account.States.Get(o.Name).Instrument = o)
        .ToList();

      var response = new ResponseModel<List<InstrumentModel>>
      {
        Data = options
      };

      return Task.FromResult(response);
    }

    /// <summary>
    /// Load account data
    /// </summary>
    public override Task<ResponseModel<IAccount>> GetAccount()
    {
      var response = new ResponseModel<IAccount>
      {
        Data = Account
      };

      return Task.FromResult(response);
    }

    /// <summary>
    /// Get all account positions
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override Task<ResponseModel<List<OrderModel>>> GetPositions(ConditionModel criteria = null)
    {
      var response = new ResponseModel<List<OrderModel>>
      {
        Data = [.. Account.Positions.Values]
      };

      return Task.FromResult(response);
    }

    /// <summary>
    /// Get all account orders
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override Task<ResponseModel<List<OrderModel>>> GetOrders(ConditionModel criteria = null)
    {
      var response = new ResponseModel<List<OrderModel>>
      {
        Data = [.. Account.Orders.Values]
      };

      return Task.FromResult(response);
    }
  }
}
