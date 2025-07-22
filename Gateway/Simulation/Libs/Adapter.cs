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
    /// Timer
    /// </summary>
    protected Timer interval;

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
    /// Subscriptions
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
      await Disconnect();

      Account.InitialBalance = Account.Balance;

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
      var scheduler = new ScheduleService();

      interval = new Timer(span);
      interval.Enabled = true;
      interval.AutoReset = true;
      interval.Elapsed += (sender, e) => scheduler.Send(() => subscriptions.Values.ForEach(o => o()), false);

      connections.Add(interval);
      connections.Add(scheduler);

      Stream += OnPoint;

      return new ResponseModel<StatusEnum>();
    }

    /// <summary>
    /// Subscribe to streams
    /// </summary>
    /// <param name="instrument"></param>
    /// <returns></returns>
    public override async Task<ResponseModel<StatusEnum>> Subscribe(InstrumentModel instrument)
    {
      await Unsubscribe(instrument);

      Account.States.Get(instrument.Name).Instrument ??= instrument;

      subscriptions[instrument.Name] = () =>
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
              states[instrument.Name] = GetState(instrument.Name, stream.Current);
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
          summary.Instrument.Point.TimeFrame = summary.TimeFrame;
          summary.Instrument.Point.Instrument = instrument;
          summary.Dom = next.Value.Dom;
          summary.Options = next.Value.Options;
          summary.Points.Add(summary.Instrument.Point);
          summary.PointGroups.Add(summary.Instrument.Point, summary.TimeFrame);

          Stream(new MessageModel<PointModel> { Next = summary.PointGroups.Last() });

          states[instrument.Name].Status = StatusEnum.Pause;
        }
      };

      return new ResponseModel<StatusEnum>();
    }

    /// <summary>
    /// Unsubscribe from streams
    /// </summary>
    /// <param name="instrument"></param>
    /// <returns></returns>
    public override Task<ResponseModel<StatusEnum>> Unsubscribe(InstrumentModel instrument)
    {
      subscriptions.TryRemove(instrument.Name, out var subscription);
      return Task.FromResult(new ResponseModel<StatusEnum>());
    }

    /// <summary>
    /// Subscribe
    /// </summary>
    public override Task<ResponseModel<StatusEnum>> Subscribe()
    {
      interval.Start();
      return base.Subscribe();
    }

    /// <summary>
    /// Unsubscribe
    /// </summary>
    public override Task<ResponseModel<StatusEnum>> Unsubscribe()
    {
      interval.Stop();
      return base.Unsubscribe();
    }

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public override async Task<ResponseModel<StatusEnum>> Disconnect()
    {
      Stream -= OnPoint;

      await Task.WhenAll(Account.States.Values.Select(o => Unsubscribe(o.Instrument)));

      connections?.ForEach(o => o?.Dispose());
      connections?.Clear();

      return new ResponseModel<StatusEnum>();
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

      nextOrder.Transaction.Id = order.Id;
      nextOrder.Transaction.Status = OrderStatusEnum.Pending;

      Account.Orders[nextOrder.Id] = nextOrder;

      return order;
    }

    /// <summary>
    /// Position opening logic 
    /// </summary>
    /// <param name="nextOrder"></param>
    /// <returns></returns>
    protected virtual OrderModel ProcessOrder(OrderModel nextOrder)
    {
      if (Account.Positions.TryGetValue(nextOrder.Name, out var currentOrder) is false)
      {
        return CreateSide(nextOrder);
      }

      if (Equals(currentOrder.Side, nextOrder.Side))
      {
        return IncreaseSide(currentOrder, nextOrder);
      }

      if ((currentOrder.Transaction.Amount - nextOrder.Amount).Is(0))
      {
        return CloseSide(currentOrder, nextOrder);
      }

      return currentOrder.Transaction.Amount < nextOrder.Amount ?
        ReverseSide(currentOrder, nextOrder) :
        ReduceSide(currentOrder, nextOrder);
    }

    /// <summary>
    /// Compute aggregated position price
    /// </summary>
    /// <param name="orders"></param>
    /// <returns></returns>
    protected virtual double? GetGroupPrice(params OrderModel[] orders)
    {
      var numerator = 0.0 as double?;
      var denominator = 0.0 as double?;

      foreach (var o in orders)
      {
        switch (true)
        {
          case true when o.Transaction.AveragePrice is null:
            numerator += o.Amount * o.Price;
            denominator += o.Amount;
            break;

          case true when o.Transaction.AveragePrice is not null:
            numerator += o.Transaction.Amount * o.Transaction.AveragePrice;
            denominator += o.Transaction.Amount;
            break;
        }
      }

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
      var closeOrder = currentOrder.Clone() as OrderModel;

      closeOrder.Transaction.Price = nextOrder.Price;

      Account.Deals.Add(closeOrder);
      Account.Positions.TryRemove(closeOrder.Name, out _);
      Account.Balance += closeOrder.GetEstimate();

      return currentOrder;
    }

    /// <summary>
    /// Increase size of the order
    /// </summary>
    /// <param name="nextOrder"></param>
    /// <returns></returns>
    protected virtual OrderModel CreateSide(OrderModel nextOrder)
    {
      var order = nextOrder.Clone() as OrderModel;

      order.Transaction.Id = nextOrder.Id;
      order.Transaction.Amount = nextOrder.Amount;
      order.Transaction.AveragePrice = nextOrder.Price;
      order.Transaction.Status = OrderStatusEnum.Filled;

      Account.Positions[order.Name] = order;

      return order;
    }

    /// <summary>
    /// Increase size of the order
    /// </summary>
    /// <param name="currentOrder"></param>
    /// <param name="nextOrder"></param>
    /// <returns></returns>
    protected virtual OrderModel IncreaseSide(OrderModel currentOrder, OrderModel nextOrder)
    {
      var order = currentOrder.Clone() as OrderModel;

      order.Transaction.AveragePrice = GetGroupPrice(currentOrder, nextOrder);
      order.Transaction.Amount = currentOrder.Transaction.Amount + nextOrder.Amount;
      order.Amount = order.Transaction.Amount;

      Account.Positions[order.Name] = order;

      return order;
    }

    /// <summary>
    /// Decrease size of the order
    /// </summary>
    /// <param name="currentOrder"></param>
    /// <param name="nextOrder"></param>
    /// <returns></returns>
    protected virtual OrderModel ReduceSide(OrderModel currentOrder, OrderModel nextOrder)
    {
      var closeOrder = currentOrder.Clone() as OrderModel;
      var order = currentOrder.Clone() as OrderModel;

      closeOrder.Transaction.Price = nextOrder.Price;
      closeOrder.Transaction.Amount = nextOrder.Amount;
      closeOrder.Amount = closeOrder.Transaction.Amount;

      order.Transaction.Amount = currentOrder.Transaction.Amount - nextOrder.Amount;
      order.Amount = order.Transaction.Amount;

      Account.Positions[order.Name] = order;
      Account.Deals.Add(closeOrder);
      Account.Balance += closeOrder.GetEstimate();

      return order;
    }

    /// <summary>
    /// Open opposite order
    /// </summary>
    /// <param name="currentOrder"></param>
    /// <param name="nextOrder"></param>
    /// <returns></returns>
    protected virtual OrderModel ReverseSide(OrderModel currentOrder, OrderModel nextOrder)
    {
      var closeOrder = currentOrder.Clone() as OrderModel;
      var order = nextOrder.Clone() as OrderModel;

      closeOrder.Transaction.Price = nextOrder.Price;
      order.Transaction.AveragePrice = nextOrder.Price;
      order.Transaction.Amount = nextOrder.Amount - currentOrder.Transaction.Amount;
      order.Transaction.Status = OrderStatusEnum.Filled;
      order.Amount = order.Transaction.Amount;

      Account.Positions[order.Name] = order;
      Account.Deals.Add(closeOrder);
      Account.Balance += closeOrder.GetEstimate();

      return order;
    }

    /// <summary>
    /// Process pending orders on each quote
    /// </summary>
    /// <param name="message"></param>
    protected virtual void OnPoint(MessageModel<PointModel> message)
    {
      var estimates = Account
        .Positions
        .Select(o => o.Value.GetEstimate())
        .ToList();

      foreach (var order in Account.Orders.Values)
      {
        if (IsOrderExecutable(order))
        {
          ProcessOrder(order);

          Account.Orders = Account
            .Orders
            .Where(o => o.Value.Instruction is not InstructionEnum.Brace && Equals(o.Value.Id, order.Id) is false)
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
      var point = order.Transaction.Instrument.Point;
      var isLong = order.Side is OrderSideEnum.Long;
      var isShort = order.Side is OrderSideEnum.Short;

      if (order.Type is OrderTypeEnum.StopLimit)
      {
        var isLongLimit = isLong && point.Ask >= order.ActivationPrice;
        var isShortLimit = isShort && point.Bid <= order.ActivationPrice;

        order.Type = isLongLimit || isShortLimit ? OrderTypeEnum.Limit : order.Type;
      }

      switch (order.Type)
      {
        case OrderTypeEnum.Stop: return isLong ? point.Ask >= order.Price : point.Bid <= order.Price;
        case OrderTypeEnum.Limit: return isLong ? point.Ask <= order.Price : point.Bid >= order.Price;
      }

      return false;
    }

    /// <summary>
    /// Parse snapshot document to get current symbol and options state
    /// </summary>
    /// <param name="name"></param>
    /// <param name="source"></param>
    /// <returns></returns>
    protected virtual SummaryModel GetState(string name, string source)
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
          return JsonSerializer.Deserialize<SummaryModel>(content, sender.Options);
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
    /// <param name="screener"></param>
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
        .Select(UpdateInstrument)
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
