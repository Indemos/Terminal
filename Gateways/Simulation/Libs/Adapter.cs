using Distribution.Services;
using SkiaSharp;
using System;
using System.Collections;
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
    public override async Task<IList<ErrorModel>> Connect()
    {
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

      Account.Positions.CollectionChanged += OnPositionUpdate;
      Account.Instruments.ForEach(async o => await Subscribe(o.Value));

      return null;
    }

    /// <summary>
    /// Subscribe to streams
    /// </summary>
    /// <param name="instrument"></param>
    /// <returns></returns>
    public override async Task<IList<ErrorModel>> Subscribe(InstrumentModel instrument)
    {
      await Unsubscribe(instrument);

      Account.Instruments[instrument.Name].Points.CollectionChanged += OnPointUpdate;

      var span = TimeSpan.FromMicroseconds(Speed);
      var points = new Dictionary<string, PointModel>();
      var scheduler = InstanceService<ScheduleService>.Instance;
      var interval = new Timer(span);

      interval.Enabled = true;
      interval.AutoReset = true;
      interval.Elapsed += (sender, e) => scheduler.Send(() =>
      {
        var point = GetSnapshot(_instruments, points);

        if (point is not null)
        {
          var instrument = Account.Instruments[point.Instrument.Name];

          point.Instrument = instrument;
          point.TimeFrame = instrument.TimeFrame;

          instrument.Points.Add(point);
          instrument.PointGroups.Add(point, instrument.TimeFrame);
        }

        interval.Enabled = true;
      });

      _subscriptions[instrument.Name] = interval;

      return null;
    }

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public override Task<IList<ErrorModel>> Disconnect()
    {
      Account.Instruments.ForEach(async o => await Unsubscribe(o.Value));
      Account.Positions.CollectionChanged -= OnPositionUpdate;

      _connections?.ForEach(o => o?.Dispose());
      _connections?.Clear();

      return Task.FromResult<IList<ErrorModel>>(null);
    }

    /// <summary>
    /// Unsubscribe from streams
    /// </summary>
    /// <param name="instrument"></param>
    /// <returns></returns>
    public override Task<IList<ErrorModel>> Unsubscribe(InstrumentModel instrument)
    {
      Account.Instruments[instrument.Name].Points.CollectionChanged -= OnPointUpdate;

      if (_subscriptions.ContainsKey(instrument.Name))
      {
        _subscriptions[instrument.Name].Dispose();
        _subscriptions.Remove(instrument.Name);
      }

      return Task.FromResult<IList<ErrorModel>>(null);
    }

    /// <summary>
    /// Create order and depending on the account, send it to the processing queue
    /// </summary>
    /// <param name="orders"></param>
    public override Task<ResponseMapModel<OrderModel>> CreateOrders(params OrderModel[] orders)
    {
      var response = ValidateOrders([.. CorrectOrders(orders)]);

      if (response.Count > 0)
      {
        return Task.FromResult(response);
      }

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
    public override Task<ResponseMapModel<OrderModel>> DeleteOrders(params OrderModel[] orders)
    {
      var response = new ResponseMapModel<OrderModel>();

      foreach (var nextOrder in orders)
      {
        Account.ActiveOrders[nextOrder.Transaction.Id].Transaction.Status = OrderStatusEnum.Canceled;
        Account.ActiveOrders.Remove(nextOrder.Transaction.Id);
      }

      return Task.FromResult(response);
    }

    /// <summary>
    /// Update order state
    /// </summary>
    /// <param name="message"></param>
    protected virtual void OnOrderUpdate(StateModel<OrderModel> message)
    {
      switch (message.Action)
      {
        case ActionEnum.Create: CreateOrders(message.Next); break;
        case ActionEnum.Delete: DeleteOrders(message.Next); break;
      }
    }

    /// <summary>
    /// Update balance after processing position
    /// </summary>
    /// <param name="message"></param>
    protected virtual void OnPositionUpdate(object sender, NotifyCollectionChangedEventArgs e)
    {
      foreach (PositionModel message in e.NewItems)
      {
        Account.Balance += message.GainLoss;
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

      Account.Orders.Add(nextOrder);
      Account.ActiveOrders.Add(nextOrder.Transaction.Id, nextOrder);

      return nextOrder;
    }

    /// <summary>
    /// Position opening logic 
    /// </summary>
    /// <param name="nextOrder"></param>
    /// <returns></returns>
    protected virtual PositionModel SendOrder(OrderModel nextOrder)
    {
      var previousPosition = Account
        .ActivePositions
        .Values
        .FirstOrDefault(o => Equals(
          o.Order.Transaction.Instrument.Name,
          nextOrder.Transaction.Instrument.Name));

      if (previousPosition is not null)
      {
        return UpdatePosition(nextOrder, previousPosition);
      }

      return CreatePosition(nextOrder);
    }

    /// <summary>
    /// Create position when there are no other positions
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    protected virtual PositionModel CreatePosition(OrderModel order)
    {
      var nextOrder = order.Clone() as OrderModel;
      var nextPosition = new PositionModel
      {
        Order = order,
        Orders = [nextOrder]
      };

      Account.Orders.Add(nextOrder);
      Account.ActivePositions.Add(nextOrder.Transaction.Id, nextPosition);

      var message = new StateModel<OrderModel>
      {
        Action = ActionEnum.Create,
        Next = nextOrder
      };

      nextOrder.Transaction.Price = nextPosition.Order.Transaction.Price = nextOrder.Price;
      nextOrder.Transaction.Status = nextPosition.Order.Transaction.Status = OrderStatusEnum.Filled;
      nextOrder.OrderStream(message);

      return nextPosition;
    }

    /// <summary>
    /// Update position  
    /// </summary>
    /// <param name="nextOrder"></param>
    /// <param name="previousPosition"></param>
    /// <returns></returns>
    protected virtual PositionModel UpdatePosition(OrderModel nextOrder, PositionModel previousPosition)
    {
      var isSameBuy = previousPosition.Order.Side is OrderSideEnum.Buy && nextOrder.Side is OrderSideEnum.Buy;
      var isSameSell = previousPosition.Order.Side is OrderSideEnum.Sell && nextOrder.Side is OrderSideEnum.Sell;

      nextOrder.Transaction.Status = OrderStatusEnum.Filled;

      if (isSameBuy || isSameSell)
      {
        return IncreasePosition(nextOrder, previousPosition);
      }

      return DecreasePosition(nextOrder, previousPosition);
    }

    /// <summary>
    /// Create position when there is a position 
    /// </summary>
    /// <param name="order"></param>
    /// <param name="previousPosition"></param>
    /// <returns></returns>
    protected virtual PositionModel IncreasePosition(OrderModel order, PositionModel previousPos)
    {
      var nextOrder = order.Clone() as OrderModel;
      var nextPosition = previousPos.Clone() as PositionModel;
      var previousPosition = previousPos.Clone() as PositionModel;

      nextOrder.Transaction.Price = order.Price;
      nextPosition.Orders = previousPosition.Orders.Concat([nextOrder]).ToList();
      nextPosition.Order.Transaction.Time = nextOrder.Transaction.Time;
      nextPosition.Order.Transaction.Volume += nextOrder.Transaction.Volume;

      nextPosition.Order.Transaction.Price =
        nextPosition.Orders.Sum(o => o.Transaction.Volume * o.Transaction.Price) /
        nextPosition.Orders.Sum(o => o.Transaction.Volume);

      nextPosition.Order.Transaction.Descriptor = string.IsNullOrEmpty(nextOrder.Transaction.Descriptor) ?
        nextPosition.Order.Transaction.Descriptor :
        nextOrder.Transaction.Descriptor;

      previousPosition.GainLoss = previousPosition.GainLossEstimate;
      previousPosition.GainLossPoints = previousPosition.GainLossPointsEstimate;

      Account.ActivePositions.Remove(previousPosition.Order.Transaction.Id);
      Account.ActivePositions.Add(nextPosition.Order.Transaction.Id, nextPosition);
      Account.Orders.Add(nextOrder);

      var message = new StateModel<OrderModel>
      {
        Action = ActionEnum.Update,
        Next = nextOrder
      };

      nextOrder.OrderStream(message);

      return nextPosition;
    }

    /// <summary>
    /// Create position when there is a position with the same transaction type 
    /// </summary>
    /// <param name="order"></param>
    /// <param name="previousPosition"></param>
    /// <returns></returns>
    protected virtual PositionModel DecreasePosition(OrderModel order, PositionModel previousPos)
    {
      var nextOrder = order.Clone() as OrderModel;
      var nextPosition = previousPos.Clone() as PositionModel;
      var previousPosition = previousPos.Clone() as PositionModel;

      nextOrder.Transaction.Price = nextOrder.Price;
      nextPosition.Orders = previousPosition.Orders.Concat([nextOrder]).ToList();
      nextPosition.Order.Transaction.Time = nextOrder.Transaction.Time;
      nextPosition.Order.Transaction.Price = nextOrder.Price;

      nextPosition.Order.Transaction.Volume = Math.Abs(
        previousPosition.Order.Transaction.Volume.Value -
        nextOrder.Transaction.Volume.Value);

      nextPosition.Order.Transaction.Descriptor = string.IsNullOrEmpty(nextOrder.Transaction.Descriptor) ?
        nextPosition.Order.Transaction.Descriptor :
        nextOrder.Transaction.Descriptor;

      previousPosition.GainLoss = previousPosition.GainLossEstimate;
      previousPosition.GainLossPoints = previousPosition.GainLossPointsEstimate;

      Account.ActivePositions.Remove(previousPosition.Order.Transaction.Id);
      Account.Positions.Add(previousPosition);
      Account.Orders.Add(nextOrder);

      var message = new StateModel<OrderModel>
      {
        Action = ActionEnum.Update,
        Next = nextOrder
      };

      if (nextPosition.Order.Transaction.Volume?.Is(0) is false)
      {
        message.Action = ActionEnum.Delete;
        Account.ActivePositions.Add(nextPosition.Order.Transaction.Id, nextPosition);
      }

      nextOrder.OrderStream(message);

      return nextPosition;
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
          .Value
          .Orders
          .Where(order => string.Equals(order.Instruction, nameof(InstructionEnum.Brace))));

      var estimates = Account.ActivePositions.Select(o => o.Value.GainLossEstimate).ToList();
      var activeOrders = Account.ActiveOrders.Values.Concat(positionOrders);

      foreach (var order in activeOrders)
      {
        var pointModel = order.Transaction.Instrument.Points.LastOrDefault();

        if (pointModel is null)
        {
          continue;
        }

        var isExecutable = false;
        var isBuyStop = order.Side is OrderSideEnum.Buy && order.Type is OrderTypeEnum.Stop;
        var isSellStop = order.Side is OrderSideEnum.Sell && order.Type is OrderTypeEnum.Stop;
        var isBuyLimit = order.Side is OrderSideEnum.Buy && order.Type is OrderTypeEnum.Limit;
        var isSellLimit = order.Side is OrderSideEnum.Sell && order.Type is OrderTypeEnum.Limit;
        var isBuyStopLimit = order.Side is OrderSideEnum.Buy && order.Type is OrderTypeEnum.StopLimit && pointModel.Ask >= order.ActivationPrice;
        var isSellStopLimit = order.Side is OrderSideEnum.Sell && order.Type is OrderTypeEnum.StopLimit && pointModel.Bid <= order.ActivationPrice;

        if (isBuyStopLimit || isSellStopLimit)
        {
          order.Type = OrderTypeEnum.Market;
        }

        if (isBuyStop || isSellLimit)
        {
          isExecutable = pointModel.Ask >= order.Transaction.Price;
        }

        if (isSellStop || isBuyLimit)
        {
          isExecutable = pointModel.Bid <= order.Transaction.Price;
        }

        if (isExecutable)
        {
          SendOrder(order);
        }
      }
    }

    /// <summary>
    /// Get next available point
    /// </summary>
    /// <param name="streams"></param>
    /// <param name="points"></param>
    /// <returns></returns>
    protected virtual PointModel GetSnapshot(
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
            points[stream.Key] = GetSnapshotContent(stream.Key, stream.Value.Current).Point;
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
    protected virtual SnapshotModel GetSnapshotContent(string name, string source)
    {
      var document = new FileInfo(source);

      if (string.Equals(document.Extension, ".zip", StringComparison.InvariantCultureIgnoreCase))
      {
        using (var stream = File.OpenRead(source))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
        using (var content = archive.Entries.First().Open())
        {
          var optionMessage = JsonSerializer.Deserialize<SnapshotModel>(content);

          optionMessage.Point.Snapshot = source;
          optionMessage.Point.Instrument = new InstrumentModel { Name = name };

          return optionMessage;
        }
      }

      var inputMessage = File.ReadAllText(source);
      var pointMessage = JsonSerializer.Deserialize<SnapshotModel>(inputMessage);

      pointMessage.Point.Snapshot = source;
      pointMessage.Point.Instrument = new InstrumentModel { Name = name };

      return pointMessage;
    }

    /// <summary>
    /// Get depth of market when available or just a top of the book
    /// </summary>
    /// <param name="screener"></param>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override Task<ResponseModel<DomModel>> GetDom(DomScreenModel screener, Hashtable criteria)
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
    public override Task<ResponseModel<IList<PointModel>>> GetPoints(PointScreenModel screener, Hashtable criteria)
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
    public override Task<ResponseModel<IList<OptionModel>>> GetOptions(OptionScreenModel screener, Hashtable criteria)
    {
      var source = $"{criteria["snapshot"]}";
      var document = new FileInfo(source);
      var response = new ResponseModel<IList<OptionModel>>();

      if (string.Equals(document.Extension, ".zip", StringComparison.InvariantCultureIgnoreCase))
      {
        using (var stream = File.OpenRead(source))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
        using (var content = archive.Entries.First().Open())
        {
          response.Data = JsonSerializer.Deserialize<SnapshotModel>(content).Options;
        }
      }

      return Task.FromResult(response);
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
    public override Task<ResponseModel<IList<PositionModel>>> GetPositions(PositionScreenModel screener, Hashtable criteria)
    {
      var response = new ResponseModel<IList<PositionModel>>
      {
        Data = [.. Account.ActivePositions.Values]
      };

      return Task.FromResult(response);
    }

    /// <summary>
    /// Get all account orders
    /// </summary>
    /// <param name="screener"></param>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public override Task<ResponseModel<IList<OrderModel>>> GetOrders(OrderScreenModel screener, Hashtable criteria)
    {
      var response = new ResponseModel<IList<OrderModel>>
      {
        Data = [.. Account.ActiveOrders.Values]
      };

      return Task.FromResult(response);
    }
  }
}
