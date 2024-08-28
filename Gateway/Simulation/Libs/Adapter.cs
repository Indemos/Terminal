using Distribution.Models;
using Distribution.Services;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
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
    /// Disposable connections
    /// </summary>
    protected IList<IDisposable> _connections;

    /// <summary>
    /// Disposable subscriptions
    /// </summary>
    protected IDictionary<string, IDisposable> _subscriptions;

    /// <summary>
    /// Instrument streams
    /// </summary>
    protected IDictionary<string, IEnumerator<string>> _instruments;

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

      _connections = [];
      _subscriptions = new Dictionary<string, IDisposable>();
      _instruments = new Dictionary<string, IEnumerator<string>>();
    }

    /// <summary>
    /// Connect
    /// </summary>
    public override async Task<ResponseModel<StatusEnum>> Connect()
    {
      var response = new ResponseModel<StatusEnum>();

      await Disconnect();

      SetupAccounts(Account);

      _instruments = Account
        .Instruments
        .ToDictionary(
          o => o.Key,
          o => Directory
            .EnumerateFiles(Path.Combine(Source, o.Value.Name), "*", SearchOption.AllDirectories)
            .GetEnumerator());

      _instruments.ForEach(o => _connections.Add(o.Value));

      Account.Instruments.ForEach(async o => await Subscribe(o.Value));

      response.Data = StatusEnum.Success;

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

      Account.Instruments[instrument.Name].Points.CollectionChanged += OnPointUpdate;

      var span = TimeSpan.FromMicroseconds(Speed);
      var points = new Dictionary<string, PointModel>();
      var scheduler = InstanceService<ScheduleService>.Instance;
      var options = new OptionModel { IsRemovable = false };
      var interval = new Timer(span);

      interval.Enabled = true;
      interval.AutoReset = false;
      interval.Elapsed += (sender, e) => scheduler.Send(() =>
      {
        var point = GetState(_instruments, points);

        if (point is not null)
        {
          var instrument = Account.Instruments[point.Instrument.Name];

          if (instrument.Points.Any())
          {
            instrument.Points.Last().Derivatives.Clear();
            instrument.PointGroups.Last().Derivatives.Clear();
          }

          point.Instrument = instrument;
          point.TimeFrame = instrument.TimeFrame;

          instrument.Point = point;
          instrument.Points.Add(point);
          instrument.PointGroups.Add(point, point.TimeFrame);

          interval.Enabled = true;
        }
      }, options);

      _subscriptions[instrument.Name] = interval;

      response.Data = StatusEnum.Success;

      return response;
    }

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public override Task<ResponseModel<StatusEnum>> Disconnect()
    {
      var response = new ResponseModel<StatusEnum>();

      Account.Instruments.ForEach(async o => await Unsubscribe(o.Value));

      _connections?.ForEach(o => o?.Dispose());
      _connections?.Clear();

      return Task.FromResult(response);
    }

    /// <summary>
    /// Unsubscribe from streams
    /// </summary>
    /// <param name="instrument"></param>
    /// <returns></returns>
    public override Task<ResponseModel<StatusEnum>> Unsubscribe(InstrumentModel instrument)
    {
      var response = new ResponseModel<StatusEnum>();

      Account.Instruments[instrument.Name].Points.CollectionChanged -= OnPointUpdate;

      if (_subscriptions.ContainsKey(instrument.Name))
      {
        _subscriptions[instrument.Name].Dispose();
        _subscriptions.Remove(instrument.Name);
      }

      return Task.FromResult(response);
    }

    /// <summary>
    /// Create order and depending on the account, send it to the processing queue
    /// </summary>
    /// <param name="orders"></param>
    public override Task<ResponseModel<IList<OrderModel>>> CreateOrders(params OrderModel[] orders)
    {
      var response = new ResponseModel<IList<OrderModel>>();

      foreach (var order in orders)
      {
        switch (order.Type)
        {
          case OrderTypeEnum.Stop:
          case OrderTypeEnum.Limit:
          case OrderTypeEnum.StopLimit: SendPendingOrder(order); break;
          case OrderTypeEnum.Market: SendOrder(order); break;
        }
      }

      return Task.FromResult(response);
    }

    /// <summary>
    /// Recursively cancel orders
    /// </summary>
    /// <param name="orders"></param>
    public override Task<ResponseModel<IList<OrderModel>>> DeleteOrders(params OrderModel[] orders)
    {
      var response = new ResponseModel<IList<OrderModel>>();
      var orderIds = orders.Select(o => o.Transaction.Id).ToList();

      Account.ActiveOrders = new ConcurrentQueue<OrderModel>(Account
        .ActiveOrders
        .Where(o => orderIds.Contains(o.Transaction.Id) is false)
        .ToList());

      return Task.FromResult(response);
    }

    /// <summary>
    /// Update order state
    /// </summary>
    /// <param name="message"></param>
    protected virtual void OnOrderUpdate(MessageModel<OrderModel> message)
    {
      switch (message.Action)
      {
        case ActionEnum.Create: CreateOrders(message.Next); break;
        case ActionEnum.Delete: DeleteOrders(message.Next); break;
      }
    }

    /// <summary>
    /// Process pending order
    /// </summary>
    /// <param name="nextOrder"></param>
    /// <returns></returns>
    protected virtual OrderModel SendPendingOrder(OrderModel nextOrder)
    {
      nextOrder.Transaction.Status = OrderStatusEnum.Placed;

      Account.ActiveOrders.Enqueue(nextOrder);

      return nextOrder;
    }

    /// <summary>
    /// Position opening logic 
    /// </summary>
    /// <param name="nextOrder"></param>
    /// <returns></returns>
    protected virtual PositionModel SendOrder(OrderModel nextOrder)
    {
      var positions = Account.ActivePositions.ToList();
      var nextPosition = CreatePosition(nextOrder);
      var groupOrders = nextPosition
        .Order
        .Orders
        .Where(o => Equals(o.Instruction, InstructionEnum.Side))
        .ToList();

      Account.ActivePositions.Clear();

      foreach (var position in positions)
      {
        UpdateSide(position.Order, nextPosition.Order);

        nextPosition
          .Order
          .Orders
          .Where(o => Equals(o.Instruction, InstructionEnum.Side))
          .ForEach(o => UpdateSide(position.Order, o));

        position
          .Order
          .Orders
          .Where(o => Equals(o.Instruction, InstructionEnum.Side))
          .ForEach(o => UpdateSide(o, nextPosition.Order));

        position
          .Order
          .Orders
          .Where(o => Equals(o.Instruction, InstructionEnum.Side))
          .ForEach(o => o
            .Orders
            .Where(subOrder => Equals(subOrder.Instruction, InstructionEnum.Side))
            .ForEach(subOrder => UpdateSide(subOrder, nextPosition.Order)));

        var sum = position.Order.GetVolume();

        switch (true)
        {
          case true when sum.Is(0): Account.Positions.Enqueue(position); break;
          case true when sum.Is(0) is false: Account.ActivePositions.Enqueue(position); break;
        }
      }

      var nextSum = nextPosition.Order.GetVolume();

      if (nextSum.Is(0) is false)
      {
        Account.ActivePositions.Enqueue(nextPosition);
      }

      Account.Orders.Enqueue(nextPosition.Order);

      return nextPosition;
    }

    /// <summary>
    /// Create position when there are no other positions
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    protected virtual PositionModel CreatePosition(OrderModel order)
    {
      void updateOrder(OrderModel o, OrderModel group = null)
      {
        o.Price = o.GetOpenEstimate();
        o.Type ??= group?.Type ?? OrderTypeEnum.Market;
        o.TimeSpan ??= group?.TimeSpan ?? OrderTimeSpanEnum.Gtc;
        o.Transaction ??= new TransactionModel();
        o.Transaction.Time ??= DateTime.Now;
        o.Transaction.Status = OrderStatusEnum.Filled;
        o.Transaction.CurrentVolume = o.Transaction.Volume;
        o.Transaction.Price = o.Price;
      }

      var position = new PositionModel();

      if (order.Transaction is not null)
      {
        updateOrder(order);
      }

      order
        .Orders
        .Where(o => Equals(o.Instruction, InstructionEnum.Side))
        .ForEach(o => updateOrder(o, order));

      position.Order = order;

      return position;
    }

    /// <summary>
    /// Compute aggregated position price
    /// </summary>
    /// <param name="orders"></param>
    /// <returns></returns>
    protected virtual double? GetGroupPrice(params OrderModel[] orders)
    {
      var numerator = orders.Sum(o => o.Transaction.CurrentVolume * o.Transaction.Price);
      var denominator = orders.Sum(o => o.Transaction.CurrentVolume);

      return numerator / denominator;
    }

    /// <summary>
    /// Match orders
    /// </summary>
    /// <param name="order"></param>
    /// <param name="update"></param>
    /// <returns></returns>
    protected virtual ResponseModel<OrderModel> UpdateSide(OrderModel order, OrderModel update)
    {
      var response = new ResponseModel<OrderModel>();
      var orderName = order.Transaction?.Instrument?.Name;
      var updateName = update.Transaction?.Instrument?.Name;

      if (Equals(updateName, orderName) is false)
      {
        response.Errors.Add(new ErrorModel { ErrorMessage = nameof(StatusEnum.Error) });
        return response;
      }

      var action = order.Transaction;

      if (action.CurrentVolume.Is(0))
      {
        response.Errors.Add(new ErrorModel { ErrorMessage = nameof(StatusEnum.Error) });
        return response;
      }

      action.Time = update.Transaction.Time;
      action.Descriptor = update.Transaction.Descriptor ?? action.Descriptor;

      if (Equals(update.Side, order.Side))
      {
        action.CurrentVolume += update.Transaction.Volume;
        action.Volume = action.CurrentVolume;
        action.Price = GetGroupPrice(order, update);
        order.Price = action.Price;
      }
      else
      {
        var volume = action.CurrentVolume - update.Transaction.Volume;

        action.CurrentVolume = Math.Abs(volume ?? 0);

        if (volume < 0)
        {
          action.Volume = action.CurrentVolume;
          action.Price = update.Price;
          order.Price = action.Price;
          order.Side = Equals(order.Side, OrderSideEnum.Buy) ? OrderSideEnum.Sell : OrderSideEnum.Buy;
        }

        if (volume.Is(0))
        {
          action.Price = update.Price;
          Account.Balance += order.GetGainEstimate();
        }
      }

      update.Transaction.CurrentVolume = 0;
      response.Data = order;

      return response;
    }

    /// <summary>
    /// Process pending orders on each quote
    /// </summary>
    /// <param name="message"></param>
    protected virtual void OnPointUpdate(object sender, NotifyCollectionChangedEventArgs e)
    {
      var positionOrders = Account
        .ActivePositions
        .SelectMany(position => position
          .Order
          .Orders
          .Where(order => Equals(order.Instruction, InstructionEnum.Brace)));

      var estimates = Account.ActivePositions.Select(o => o.GetGainEstimate()).ToList();
      var activeOrders = Account.ActiveOrders.Concat(positionOrders);

      foreach (var order in activeOrders)
      {
        var point = order.Transaction.Instrument.Points.LastOrDefault();

        if (Equals(order.Instruction, InstructionEnum.Group))
        {
          if (GetGroupOrderState(order))
          {
            SendOrder(order);
          }

          continue;
        }

        if (GetOrderState(order, point))
        {
          SendOrder(order);
        }
      }
    }

    /// <summary>
    /// Check if group of pending orders can be executed
    /// </summary>
    /// <param name="groupOrder"></param>
    /// <returns></returns>
    protected virtual bool GetGroupOrderState(OrderModel groupOrder)
    {
      var executable = true;
      var groupPoint = groupOrder.Transaction.Instrument.Points.LastOrDefault();
      var groupOrders = groupOrder.Orders.Where(o => Equals(o.Instruction, InstructionEnum.Side));

      foreach (var order in groupOrders)
      {
        var point = order.Transaction.Instrument.Points.LastOrDefault();

        if (Equals(order.Transaction.Instrument.Type, InstrumentTypeEnum.Options))
        {
          executable = executable && GetOrderState(order, GetPointOption(order, groupPoint).Point);
          continue;
        }

        executable = executable && GetOrderState(order, groupPoint);
      }

      return executable;
    }

    /// <summary>
    /// Check if pending order can be executed
    /// </summary>
    /// <param name="order"></param>
    /// <param name="point"></param>
    /// <returns></returns>
    protected virtual bool GetOrderState(OrderModel order, PointModel point)
    {
      var isExecutable = false;
      var isBuyStop = order.Side is OrderSideEnum.Buy && order.Type is OrderTypeEnum.Stop;
      var isSellStop = order.Side is OrderSideEnum.Sell && order.Type is OrderTypeEnum.Stop;
      var isBuyLimit = order.Side is OrderSideEnum.Buy && order.Type is OrderTypeEnum.Limit;
      var isSellLimit = order.Side is OrderSideEnum.Sell && order.Type is OrderTypeEnum.Limit;
      var isBuyStopLimit = order.Side is OrderSideEnum.Buy && order.Type is OrderTypeEnum.StopLimit && point.Ask >= order.ActivationPrice;
      var isSellStopLimit = order.Side is OrderSideEnum.Sell && order.Type is OrderTypeEnum.StopLimit && point.Bid <= order.ActivationPrice;

      if (isBuyStopLimit || isSellStopLimit)
      {
        order.Type = OrderTypeEnum.Limit;
      }

      if (isBuyStop || isSellLimit)
      {
        isExecutable = point.Ask >= order.Price;
      }

      if (isSellStop || isBuyLimit)
      {
        isExecutable = point.Bid <= order.Price;
      }

      return isExecutable;
    }

    /// <summary>
    /// Get next available point
    /// </summary>
    /// <param name="streams"></param>
    /// <param name="points"></param>
    /// <returns></returns>
    protected virtual PointModel GetState(
      IDictionary<string, IEnumerator<string>> streams,
      IDictionary<string, PointModel> points)
    {
      var index = string.Empty;

      foreach (var stream in streams)
      {
        points.TryGetValue(stream.Key, out PointModel point);

        if (point is null)
        {
          stream.Value.MoveNext();

          if (string.IsNullOrEmpty(stream.Value.Current) is false)
          {
            points[stream.Key] = GetStateContent(stream.Key, stream.Value.Current);
          }
        }

        points.TryGetValue(index, out PointModel min);
        points.TryGetValue(stream.Key, out PointModel current);

        var isOne = string.IsNullOrEmpty(index);
        var isMin = current is not null && min is not null && current.Time <= min.Time;

        if (isOne || isMin)
        {
          index = stream.Key;
        }
      }

      var response = points[index];

      points[index] = null;

      return response;
    }

    /// <summary>
    /// Parse snapshot document to get current symbol and options state
    /// </summary>
    /// <param name="name"></param>
    /// <param name="source"></param>
    /// <returns></returns>
    protected virtual PointModel GetStateContent(string name, string source)
    {
      var document = new FileInfo(source);

      if (string.Equals(document.Extension, ".zip", StringComparison.InvariantCultureIgnoreCase))
      {
        using (var stream = File.OpenRead(source))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
        using (var content = archive.Entries.First().Open())
        {
          var optionMessage = JsonSerializer.Deserialize<PointModel>(content);
          optionMessage.Instrument = new InstrumentModel { Name = name };
          return optionMessage;
        }
      }

      var inputMessage = File.ReadAllText(source);
      var pointMessage = JsonSerializer.Deserialize<PointModel>(inputMessage);

      pointMessage.Instrument = new InstrumentModel { Name = name };

      return pointMessage;
    }

    /// <summary>
    /// Get depth of market when available or just a top of the book
    /// </summary>
    /// <param name="screener"></param>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override Task<ResponseModel<DomModel>> GetDom(DomScreenerModel screener, Hashtable criteria)
    {
      var point = Account.Instruments[screener.Name].Points.LastOrDefault();
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
    public override Task<ResponseModel<IList<PointModel>>> GetPoints(PointScreenerModel screener, Hashtable criteria)
    {
      var response = new ResponseModel<IList<PointModel>>
      {
        Data = [.. Account.Instruments[screener.Name].Points]
      };

      return Task.FromResult(response);
    }

    /// <summary>
    /// Option chain
    /// </summary>
    /// <param name="screener"></param>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override Task<ResponseModel<IList<InstrumentModel>>> GetOptions(OptionScreenerModel screener, Hashtable criteria)
    {
      return Task.FromResult(GetPointOptions(screener, criteria));
    }

    /// <summary>
    /// Check if group of pending orders can be executed
    /// </summary>
    /// <param name="order"></param>
    /// <param name="point"></param>
    /// <returns></returns>
    protected virtual InstrumentModel GetPointOption(OrderModel order, PointModel point)
    {
      var option = order.Transaction.Instrument.Derivative;
      var optionScreener = new OptionScreenerModel
      {
        Point = point,
        Side = option.Side,
        MinPrice = option.Strike,
        MaxPrice = option.Strike,
        MinDate = option.Expiration,
        MaxDate = option.Expiration
      };

      return GetPointOptions(optionScreener, []).Data.FirstOrDefault();
    }

    /// <summary>
    /// Option chain
    /// </summary>
    /// <param name="screener"></param>
    /// <param name="criteria"></param>
    /// <returns></returns>
    protected virtual ResponseModel<IList<InstrumentModel>> GetPointOptions(OptionScreenerModel screener, Hashtable criteria)
    {
      var response = new ResponseModel<IList<InstrumentModel>>
      {
        Data = screener
        .Point
        .Derivatives[nameof(InstrumentTypeEnum.Options)]
        .Where(o => screener?.Side is null || Equals(o.Derivative.Side, screener?.Side))
        .Where(o => screener?.MinPrice is null || o.Derivative.Strike >= screener?.MinPrice)
        .Where(o => screener?.MaxPrice is null || o.Derivative.Strike <= screener?.MaxPrice)
        .Where(o => screener?.MinDate is null || o.Derivative.Expiration >= screener?.MinDate)
        .Where(o => screener?.MaxDate is null || o.Derivative.Expiration <= screener?.MaxDate)
        .ToList()
      };

      return response;
    }

    /// <summary>
    /// Load account data
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override Task<ResponseModel<IAccount>> GetAccount(Hashtable criteria)
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
    /// <param name="screener"></param>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override Task<ResponseModel<IList<PositionModel>>> GetPositions(PositionScreenerModel screener, Hashtable criteria)
    {
      var response = new ResponseModel<IList<PositionModel>>
      {
        Data = [.. Account.ActivePositions]
      };

      return Task.FromResult(response);
    }

    /// <summary>
    /// Get all account orders
    /// </summary>
    /// <param name="screener"></param>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override Task<ResponseModel<IList<OrderModel>>> GetOrders(OrderScreenerModel screener, Hashtable criteria)
    {
      var response = new ResponseModel<IList<OrderModel>>
      {
        Data = [.. Account.ActiveOrders]
      };

      return Task.FromResult(response);
    }
  }
}
