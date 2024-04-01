using Distribution.Services;
using Mapper;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
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
    protected IDictionary<string, StreamReader> _instruments;

    /// <summary>
    /// Simulation speed in milliseconds
    /// </summary>
    public int Speed { get; set; }

    /// <summary>
    /// Location of the files with quotes
    /// </summary>
    public string Source { get; set; }

    public ScheduleService Scheduler { get; set; } = new();

    /// <summary>
    /// Constructor
    /// </summary>
    public Adapter()
    {
      Speed = 1000;

      _connections = new List<IDisposable>();
      _instruments = new Dictionary<string, StreamReader>();
      _subscriptions = new Dictionary<string, IDisposable>();
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
        .ToDictionary(o => o.Key, o => new StreamReader(Path.Combine(Source, o.Value.Name)));

      _instruments.ForEach(o => _connections.Add(o.Value));

      Account.Positions.CollectionChanged += OnPositionUpdate;
      Account.Instruments.ForEach(async o => await Subscribe(o.Key));

      return null;
    }

    /// <summary>
    /// Subscribe to data streams
    /// </summary>
    public override async Task<IList<ErrorModel>> Subscribe(string name)
    {
      await Unsubscribe(name);

      Account.Instruments[name].Points.CollectionChanged += OnPointUpdate;

      var span = TimeSpan.FromMicroseconds(Speed);
      var points = new Dictionary<string, PointModel?>();
      var scheduler = InstanceService<ScheduleService>.Instance;
      var interval = new Timer(span);
      
      interval.Enabled = true;
      interval.AutoReset = true;
      interval.Elapsed += (sender, e) => scheduler.Send(() =>
      {
        var point = GetRecord(_instruments, points);

        if (point is not null)
        {
          SetupPoints(point);
        }

        interval.Enabled = true;
      });

      _subscriptions[name] = interval;

      return null;
    }

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public override Task<IList<ErrorModel>> Disconnect()
    {
      Account.Instruments.ForEach(async o => await Unsubscribe(o.Key));
      Account.Positions.CollectionChanged -= OnPositionUpdate;

      _connections?.ForEach(o => o.Dispose());
      _connections?.Clear();

      return Task.FromResult<IList<ErrorModel>>(null);
    }

    /// <summary>
    /// Unsubscribe from data streams
    /// </summary>
    public override Task<IList<ErrorModel>> Unsubscribe(string name)
    {
      Account.Instruments[name].Points.CollectionChanged -= OnPointUpdate;

      if (_subscriptions.ContainsKey(name))
      {
        _subscriptions[name].Dispose();
        _subscriptions.Remove(name);
      }

      return Task.FromResult<IList<ErrorModel>>(null);
    }

    /// <summary>
    /// Get quote
    /// </summary>
    /// <param name="message"></param>
    public override Task<ResponseItemModel<PointModel?>> GetPoint(PointMessageModel message)
    {
      return Task.FromResult(new ResponseItemModel<PointModel?>
      {
        Data = Account.Instruments[message.Name].Points.LastOrDefault()
      });
    }

    /// <summary>
    /// Create order and depending on the account, send it to the processing queue
    /// </summary>
    /// <param name="orders"></param>
    public override Task<ResponseMapModel<OrderModel>> CreateOrders(params OrderModel[] orders)
    {
      var response = ValidateOrders(CorrectOrders(orders).ToArray());

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
    /// Update orders
    /// </summary>
    /// <param name="orders"></param>
    public override Task<ResponseMapModel<OrderModel>> UpdateOrders(params OrderModel[] orders)
    {
      var nextOrders = orders.Select(nextOrder =>
      {
        return Mapper<OrderModel, OrderModel>.Merge(
          nextOrder,
          Account.ActiveOrders[nextOrder.Transaction?.Id].Value);

      }).ToArray();

      var response = ValidateOrders(nextOrders);

      if (response.Count > 0)
      {
        return Task.FromResult(response);
      }

      foreach (var nextOrder in nextOrders)
      {
        Account.ActiveOrders[nextOrder.Transaction?.Id] = nextOrder;
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

      foreach (var order in orders)
      {
        var nextOrder = order;
        var action = nextOrder.Transaction.Value;

        action.Status = OrderStatusEnum.Canceled;
        nextOrder.Transaction = action;
        Account.ActiveOrders.Remove(nextOrder.Transaction?.Id);
      }

      return Task.FromResult(response);
    }

    /// <summary>
    /// Update order state
    /// </summary>
    /// <param name="message"></param>
    protected void OnOrderUpdate(StateModel<OrderModel> message)
    {
      switch (message.Action)
      {
        case ActionEnum.Create: CreateOrders(message.Next); break;
        case ActionEnum.Update: UpdateOrders(message.Next); break;
        case ActionEnum.Delete: DeleteOrders(message.Next); break;
      }
    }

    /// <summary>
    /// Update balance after processing position
    /// </summary>
    /// <param name="message"></param>
    protected void OnPositionUpdate(object sender, NotifyCollectionChangedEventArgs e)
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
    protected OrderModel SendPendingOrder(OrderModel nextOrder)
    {
      var action = nextOrder.Transaction.Value;

      action.Status = OrderStatusEnum.Placed;
      nextOrder.Transaction = action;

      Account.Orders.Add(nextOrder);
      Account.ActiveOrders.Add(nextOrder.Transaction?.Id, nextOrder);

      return nextOrder;
    }

    /// <summary>
    /// Position opening logic 
    /// </summary>
    /// <param name="nextOrder"></param>
    /// <returns></returns>
    protected PositionModel SendOrder(OrderModel nextOrder)
    {
      var previousPosition = Account
        .ActivePositions
        .Values
        .FirstOrDefault(o => Equals(
          o?.Order?.Transaction?.Instrument.Name,
          nextOrder.Transaction?.Instrument.Name));

      if (previousPosition is not null)
      {
        return UpdatePosition(nextOrder, previousPosition.Value);
      }

      return CreatePosition(nextOrder);
    }

    /// <summary>
    /// Create position when there are no other positions
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    protected PositionModel CreatePosition(OrderModel nextOrder)
    {
      var action = nextOrder.Transaction.Value;

      action.Status = OrderStatusEnum.Filled;
      action.Price ??= GetOpenPrice(nextOrder);
      nextOrder.Transaction = action;

      var nextPosition = new PositionModel
      {
        Order = nextOrder,
        Orders = new List<OrderModel?>
        {
          nextOrder
        }
      };

      Account.Orders.Add(nextOrder);
      Account.ActivePositions.Add(nextOrder.Transaction?.Id, nextPosition);

      var message = new StateModel<OrderModel>
      {
        Action = ActionEnum.Create,
        Next = nextOrder
      };

      nextOrder.OrderStream(message);

      return nextPosition;
    }

    /// <summary>
    /// Update position  
    /// </summary>
    /// <param name="nextOrder"></param>
    /// <param name="previousPosition"></param>
    /// <returns></returns>
    protected PositionModel UpdatePosition(OrderModel nextOrder, PositionModel previousPosition)
    {
      var isSameBuy = previousPosition.Order?.Side is OrderSideEnum.Buy && nextOrder.Side is OrderSideEnum.Buy;
      var isSameSell = previousPosition.Order?.Side is OrderSideEnum.Sell && nextOrder.Side is OrderSideEnum.Sell;
      var action = nextOrder.Transaction.Value;

      action.Status = OrderStatusEnum.Filled;
      nextOrder.Transaction = action;

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
    protected PositionModel IncreasePosition(OrderModel order, PositionModel previousPosition)
    {
      var nextOrder = order;
      var nextPosition = previousPosition;
      var action = nextPosition.Order?.Transaction.Value ?? new TransactionModel();

      nextPosition.Orders = previousPosition.Orders.ToList();
      nextPosition.Orders.Add(nextOrder);
      action.Id = nextOrder.Transaction?.Id;
      action.Time = nextOrder.Transaction?.Time;
      action.Volume += nextOrder.Transaction?.Volume;
      action.Price =
        nextPosition.Orders.Sum(o => o?.Transaction?.Volume * o?.Transaction?.Price) /
        nextPosition.Orders.Sum(o => o?.Transaction?.Volume);

      previousPosition.GainLoss = previousPosition.GainLossEstimate;
      previousPosition.GainLossPoints = previousPosition.GainLossPointsEstimate;

      Account.ActivePositions.Remove(previousPosition.Order?.Transaction?.Id);
      Account.ActivePositions.Add(nextPosition.Order?.Transaction?.Id, nextPosition);
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
    protected PositionModel DecreasePosition(OrderModel order, PositionModel previousPosition)
    {
      var nextOrder = order;
      var nextPosition = previousPosition;
      var action = nextOrder.Transaction.Value;
      var volumeUpdate = previousPosition.Order?.Transaction?.Volume - action.Volume;

      action.Volume = Math.Abs(volumeUpdate.Value);
      nextOrder.Transaction = action;
      nextPosition.Orders = previousPosition.Orders.ToList();
      nextPosition.Orders.Add(order);
      nextPosition.Order = nextOrder;

      previousPosition.GainLoss = previousPosition.GainLossEstimate;
      previousPosition.GainLossPoints = previousPosition.GainLossPointsEstimate;

      Account.ActivePositions.Remove(previousPosition.Order?.Transaction?.Id);
      Account.Positions.Add(previousPosition);
      Account.Orders.Add(order);

      var message = new StateModel<OrderModel>
      {
        Action = ActionEnum.Update,
        Next = order
      };

      if (action.Volume?.IsEqual(0) is false)
      {
        message.Action = ActionEnum.Delete;
        Account.ActivePositions.Add(action.Id, nextPosition);
      }

      order.OrderStream(message);

      return nextPosition;
    }

    /// <summary>
    /// Process pending orders on each quote
    /// </summary>
    /// <param name="message"></param>
    protected void OnPointUpdate(object sender, NotifyCollectionChangedEventArgs e)
    {
      var x = Account.ActiveOrders.Values;

      var positionOrders = Account.ActivePositions.SelectMany(o => o.Value?.Orders);
      var activeOrders = Account.ActiveOrders.Values.Concat(positionOrders);

      foreach (var nextOrder in activeOrders)
      {
        var order = nextOrder.Value;
        var pointModel = order.Transaction?.Instrument.Points.LastOrDefault();

        if (pointModel is null)
        {
          continue;
        }

        var isExecutable = false;
        var isBuyStop = order.Side is OrderSideEnum.Buy && order.Type is OrderTypeEnum.Stop;
        var isSellStop = order.Side is OrderSideEnum.Sell && order.Type is OrderTypeEnum.Stop;
        var isBuyLimit = order.Side is OrderSideEnum.Buy && order.Type is OrderTypeEnum.Limit;
        var isSellLimit = order.Side is OrderSideEnum.Sell && order.Type is OrderTypeEnum.Limit;
        var isBuyStopLimit = order.Side is OrderSideEnum.Buy && order.Type is OrderTypeEnum.StopLimit && pointModel?.Ask >= order.ActivationPrice;
        var isSellStopLimit = order.Side is OrderSideEnum.Sell && order.Type is OrderTypeEnum.StopLimit && pointModel?.Bid <= order.ActivationPrice;

        if (isBuyStopLimit || isSellStopLimit)
        {
          order.Type = OrderTypeEnum.Market;
        }

        if (isBuyStop || isSellLimit)
        {
          isExecutable = pointModel?.Ask >= order.Transaction?.Price;
        }

        if (isSellStop || isBuyLimit)
        {
          isExecutable = pointModel?.Bid <= order.Transaction?.Price;
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
    /// <returns></returns>
    protected PointModel? GetRecord(IDictionary<string, StreamReader> streams, IDictionary<string, PointModel?> points)
    {
      var index = string.Empty;

      foreach (var stream in streams)
      {
        points.TryGetValue(stream.Key, out PointModel? point);

        if (point is null)
        {
          var input = stream.Value.ReadLine();

          if (string.IsNullOrEmpty(input) is false)
          {
            points[stream.Key] = Parse(stream.Key, input);
          }
        }

        points.TryGetValue(index, out PointModel? min);
        points.TryGetValue(stream.Key, out PointModel? current);

        var isOne = string.IsNullOrEmpty(index);
        var isMin = current is not null && min is not null && current?.Time <= min?.Time;

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
    /// Parse point
    /// </summary>
    /// <param name="name"></param>
    /// <param name="input"></param>
    /// <returns></returns>
    protected PointModel? Parse(string name, string input)
    {
      var props = input.Split(" ");

      long.TryParse(props.ElementAtOrDefault(0), out long date);

      if (date is 0)
      {
        return null;
      }

      double.TryParse(props.ElementAtOrDefault(1), out double bid);
      double.TryParse(props.ElementAtOrDefault(2), out double bidSize);
      double.TryParse(props.ElementAtOrDefault(3), out double ask);
      double.TryParse(props.ElementAtOrDefault(4), out double askSize);

      var response = new PointModel
      {
        Time = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(date),
        Ask = ask,
        Bid = bid,
        Price = ask,
        AskSize = askSize,
        BidSize = bidSize,
        Instrument = new Instrument()
        {
          Name = name
        }
      };

      if (askSize.IsEqual(0))
      {
        response.Price = bid;
      }

      return response;
    }

    /// <summary>
    /// Price estimate
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    protected virtual double? GetOpenPrice(OrderModel? order)
    {
      var point = order?.Transaction?.Instrument.Points.LastOrDefault();

      if (point is not null)
      {
        switch (order?.Side)
        {
          case OrderSideEnum.Buy: return point?.Ask;
          case OrderSideEnum.Sell: return point?.Bid;
        }
      }

      return null;
    }


    public override Task<ResponseItemModel<IList<PointModel?>>> GetPoints(PointMessageModel message)
    {
      throw new NotImplementedException();
    }

    public override Task<ResponseItemModel<IList<OptionModel>>> GetOptions(OptionMessageModel message)
    {
      throw new NotImplementedException();
    }
  }
}
