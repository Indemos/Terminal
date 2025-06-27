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
using Terminal.Core.Validators;

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
    protected ConcurrentDictionary<string, IDisposable> subscriptions;

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
        .State
        .ToDictionary(
          o => o.Key,
          o => Directory
            .EnumerateFiles(Path.Combine(Source, o.Value.Instrument.Name), "*", SearchOption.AllDirectories)
            .GetEnumerator())
            .Concurrent();

      streams.ForEach(o => connections.Add(o.Value));

      await Task.WhenAll(Account.State.Values.Select(o => Subscribe(o.Instrument)));

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

      Account.State.Get(instrument.Name).Instrument ??= instrument;

      Stream += OnPoint;

      var span = TimeSpan.FromMicroseconds(Speed);
      var scheduler = InstanceService<ScheduleService>.Instance;
      var interval = new Timer(span);

      interval.Enabled = true;
      interval.AutoReset = true;
      interval.Elapsed += (sender, e) => scheduler.Send(async () =>
      {
        states.TryGetValue(instrument.Name, out var state);
        streams.TryGetValue(instrument.Name, out var stream);

        if (state is null || state.Status is StatusEnum.Suspended)
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

        var counter = 1;
        var next = states
          .Where(o => o.Value.Status is StatusEnum.Active or StatusEnum.Inactive)
          .Aggregate((min, o) =>
          {
            counter++;
            return o.Value.Instrument.Point.Time <= min.Value.Instrument.Point.Time ? o : min;
          });

        if (Equals(counter, streams.Count) && Equals(next.Key, instrument.Name))
        {
          var summary = Account.State.Get(instrument.Name);

          summary.Instrument = instrument;
          summary.Instrument.Point = next.Value.Instrument.Point;
          summary.Instrument.Point.Bar = null;
          summary.Instrument.Point.Instrument = instrument;
          summary.Dom = next.Value.Dom;
          summary.Options = next.Value.Options;
          summary.Points.Add(summary.Instrument.Point);
          summary.PointGroups.Add(summary.Instrument.Point, instrument.TimeFrame);

          Stream(new MessageModel<PointModel> { Next = summary.PointGroups.Last() });

          states[instrument.Name].Status = StatusEnum.Suspended;
        }
      });

      subscriptions[instrument.Name] = interval;

      response.Data = StatusEnum.Active;

      return response;
    }

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public override async Task<ResponseModel<StatusEnum>> Disconnect()
    {
      var response = new ResponseModel<StatusEnum>();

      await Task.WhenAll(Account.State.Values.Select(o => Unsubscribe(o.Instrument)));

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

      Stream -= OnPoint;

      if (subscriptions.TryRemove(instrument.Name, out var subscription))
      {
        subscription.Dispose();
      }

      return Task.FromResult(response);
    }

    /// <summary>
    /// Create order and depending on the account, send it to the processing queue
    /// </summary>
    /// <param name="orders"></param>
    public override Task<ResponseModel<List<OrderModel>>> SendOrders(params OrderModel[] orders)
    {
      var response = new ResponseModel<List<OrderModel>> { Data = [] };
      var validator = InstanceService<OrderPriceValidator>.Instance;

      response.Errors = [.. orders.SelectMany(o => validator
        .Validate(o)
        .Errors
        .Select(error => new ErrorModel { ErrorMessage = error.ErrorMessage }))];

      if (response.Errors.Count is not 0)
      {
        return Task.FromResult(response);
      }

      foreach (var order in orders)
      {
        var nextOrders = ComposeOrders(order);

        foreach (var nextOrder in nextOrders)
        {
          switch (nextOrder.Type)
          {
            case OrderTypeEnum.Stop:
            case OrderTypeEnum.Limit:
            case OrderTypeEnum.StopLimit: SendPendingOrder(nextOrder); break;
            case OrderTypeEnum.Market: SendOrder(nextOrder); break;
          }

          nextOrder.Orders.ForEach(o => SendPendingOrder(o));
          response.Data.Add(nextOrder);
        }
      }

      return Task.FromResult(response);
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
    protected virtual OrderModel SendOrder(OrderModel order)
    {
      var nextOrder = order.Clone() as OrderModel;

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
          SendOrder(order);
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
      var point = Account.State.Get(criteria.Instrument.Name).Instrument.Point;
      var response = new ResponseModel<DomModel>
      {
        Data = new DomModel
        {
          Bids = [point],
          Asks = [point]
        }
      };

      return Task.FromResult(response);
    }

    /// <summary>
    /// List of points by criteria, e.g. for specified instrument
    /// </summary>
    /// <param name="screener"></param>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override Task<ResponseModel<List<PointModel>>> GetPoints(ConditionModel criteria = null)
    {
      var response = new ResponseModel<List<PointModel>>
      {
        Data = [.. Account.State.Get(criteria.Instrument.Name).Points]
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

      var orderMap = Account
        .Positions
        .Values
        .SelectMany(o => o.Orders.Append(o))
        .GroupBy(o => o.Instrument.Name)
        .ToDictionary(o => o.Key, o => o);

      var options = Account
        .State
        .Get(criteria.Instrument.Name)
        .Options
        .Select(option =>
        {
          if (orderMap.TryGetValue(option.Name, out var orders))
          {
            orders.ForEach(o => o.Instrument = option);
          }

          return option;
        })
        .Where(o => side is null || Equals(o.Derivative.Side, side))
        .Where(o => criteria?.MinDate is null || o.Derivative.ExpirationDate?.Date >= criteria.MinDate?.Date)
        .Where(o => criteria?.MaxDate is null || o.Derivative.ExpirationDate?.Date <= criteria.MaxDate?.Date)
        .Where(o => criteria?.MinPrice is null || o.Derivative.Strike >= criteria.MinPrice)
        .Where(o => criteria?.MaxPrice is null || o.Derivative.Strike <= criteria.MaxPrice)
        .OrderBy(o => o.Derivative.ExpirationDate)
        .ThenBy(o => o.Derivative.Strike)
        .ThenBy(o => o.Derivative.Side)
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
