using FluentValidation.Results;
using Mapper;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Extensions;
using Terminal.Core.Models;

namespace Terminal.Connector.Simulation
{
  public class Adapter : Core.Domains.Connector, IDisposable
  {
    /// <summary>
    /// Disposable connections
    /// </summary>
    protected IList<IDisposable> _connections;

    /// <summary>
    /// Disposable subscriptions
    /// </summary>
    protected IList<IDisposable> _subscriptions;

    /// <summary>
    /// Instrument streams
    /// </summary>
    protected IDictionary<string, StreamReader> _instruments;

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
      Speed = 100;

      _connections = new List<IDisposable>();
      _subscriptions = new List<IDisposable>();
      _instruments = new Dictionary<string, StreamReader>();
    }

    /// <summary>
    /// Connect
    /// </summary>
    public override async Task<IList<ErrorModel>> Connect()
    {
      await Disconnect();

      CorrectAccounts(Account);

      _instruments = Account
        .Instruments
        .ToDictionary(o => o.Key, o => new StreamReader(Path.Combine(Source, o.Value.Name)));

      _instruments.ForEach(o => _connections.Add(o.Value));

      await Subscribe();

      return null;
    }

    /// <summary>
    /// Subscribe to data streams
    /// </summary>
    public override async Task<IList<ErrorModel>> Subscribe()
    {
      await Unsubscribe();

      OrderStream += OnOrderUpdate;
      Account.Positions.CollectionChanged += OnPositionUpdate;
      Account.Instruments.ForEach(o => o.Value.Points.CollectionChanged += OnPointUpdate);

      var span = TimeSpan.FromMilliseconds(Speed);
      var points = new Dictionary<string, PointModel>();
      var scheduler = new EventLoopScheduler();
      var interval = Observable
        .Interval(span, scheduler)
        .Subscribe(o =>
        {
          var point = GetRecord(_instruments, points);

          if (point is not null)
          {
            CorrectPoints(point);
          }
        });

      _subscriptions.Add(interval);

      return null;
    }

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public override Task<IList<ErrorModel>> Disconnect()
    {
      Unsubscribe();

      _connections?.ForEach(o => o.Dispose());
      _connections?.Clear();

      return Task.FromResult<IList<ErrorModel>>(null);
    }

    /// <summary>
    /// Unsubscribe from data streams
    /// </summary>
    public override Task<IList<ErrorModel>> Unsubscribe()
    {
      OrderStream -= OnOrderUpdate;
      Account.Positions.CollectionChanged -= OnPositionUpdate;
      Account.Instruments.ForEach(o => o.Value.Points.CollectionChanged -= OnPointUpdate);

      _subscriptions?.ForEach(o => o.Dispose());
      _subscriptions?.Clear();

      return Task.FromResult<IList<ErrorModel>>(null);
    }

    /// <summary>
    /// Get quote
    /// </summary>
    /// <param name="message"></param>
    public override Task<ResponseItemModel<PointModel>> GetPoint(PointMessageModel message)
    {
      return Task.FromResult(new ResponseItemModel<PointModel>
      {
        Data = Account.Instruments[message.Name].Points.LastOrDefault()
      });
    }

    /// <summary>
    /// Create order and depending on the account, send it to the processing queue
    /// </summary>
    /// <param name="orders"></param>
    public override Task<ResponseModel<OrderModel>> CreateOrders(params OrderModel[] orders)
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
    public override Task<ResponseModel<OrderModel>> UpdateOrders(params OrderModel[] orders)
    {
      var nextOrders = orders.Select(nextOrder =>
      {
        return Mapper<OrderModel, OrderModel>.Merge(
          nextOrder,
          Account.ActiveOrders[nextOrder.Transaction.Id].Clone() as OrderModel);

      }).ToArray();

      var response = ValidateOrders(nextOrders);

      if (response.Count > 0)
      {
        return Task.FromResult(response);
      }

      foreach (var nextOrder in nextOrders)
      {
        Account.ActiveOrders[nextOrder.Transaction.Id] = nextOrder;
      }

      return Task.FromResult(response);
    }

    /// <summary>
    /// Recursively cancel orders
    /// </summary>
    /// <param name="orders"></param>
    public override Task<ResponseModel<OrderModel>> DeleteOrders(params OrderModel[] orders)
    {
      var response = new ResponseModel<OrderModel>();

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
        case ActionEnum.Update: UpdateOrders(message.Next); break;
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
        Orders = new List<OrderModel>
        {
          nextOrder
        }
      };

      Account.Orders.Add(nextOrder);
      Account.ActivePositions.Add(nextOrder.Transaction.Id, nextPosition);

      var message = new StateModel<OrderModel>
      {
        Action = ActionEnum.Create,
        Next = nextOrder
      };

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
      var isSameBuy = Equals(previousPosition.Order.Side, OrderSideEnum.Buy) && Equals(nextOrder.Side, OrderSideEnum.Buy);
      var isSameSell = Equals(previousPosition.Order.Side, OrderSideEnum.Sell) && Equals(nextOrder.Side, OrderSideEnum.Sell);

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
    protected virtual PositionModel IncreasePosition(OrderModel order, PositionModel previousPosition)
    {
      var nextOrder = order.Clone() as OrderModel;
      var nextPosition = previousPosition.Clone() as PositionModel;

      nextPosition.Orders = previousPosition.Orders.Concat(new[] { nextOrder }).ToList();
      nextPosition.Order.Transaction.Id = nextOrder.Transaction.Id;
      nextPosition.Order.Transaction.Time = nextOrder.Transaction.Time;
      nextPosition.Order.Transaction.Volume += nextOrder.Transaction.Volume;
      nextPosition.Order.Transaction.Price =
        nextPosition.Orders.Sum(o => o.Transaction.Volume * o.Transaction.Price) /
        nextPosition.Orders.Sum(o => o.Transaction.Volume);

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
    protected virtual PositionModel DecreasePosition(OrderModel order, PositionModel previousPosition)
    {
      var nextOrder = order.Clone() as OrderModel;
      var nextPosition = previousPosition.Clone() as PositionModel;

      nextPosition.Orders = previousPosition.Orders.Concat(new[] { nextOrder }).ToList();
      nextPosition.Order.Transaction.Id = nextOrder.Transaction.Id;
      nextPosition.Order.Transaction.Time = nextOrder.Transaction.Time;
      nextPosition.Order.Transaction.Price = nextOrder.Transaction.Price;
      nextPosition.Order.Transaction.Volume = Math.Abs(
        previousPosition.Order.Transaction.Volume.Value -
        nextOrder.Transaction.Volume.Value);

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

      if (nextPosition.Order.Transaction.Volume?.IsEqual(0) is false)
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
      var positionOrders = Account.ActivePositions.SelectMany(o => o.Value.Orders);
      var activeOrders = Account.ActiveOrders.Values.Concat(positionOrders);

      foreach (var order in activeOrders)
      {
        var pointModel = order.Transaction.Instrument.Points.LastOrDefault();

        if (pointModel is null)
        {
          continue;
        }

        var isExecutable = false;
        var isBuyStop = Equals(order.Side, OrderSideEnum.Buy) && Equals(order.Type, OrderTypeEnum.Stop);
        var isSellStop = Equals(order.Side, OrderSideEnum.Sell) && Equals(order.Type, OrderTypeEnum.Stop);
        var isBuyLimit = Equals(order.Side, OrderSideEnum.Buy) && Equals(order.Type, OrderTypeEnum.Limit);
        var isSellLimit = Equals(order.Side, OrderSideEnum.Sell) && Equals(order.Type, OrderTypeEnum.Limit);
        var isBuyStopLimit = Equals(order.Side, OrderSideEnum.Buy) && Equals(order.Type, OrderTypeEnum.StopLimit) && pointModel.Ask >= order.ActivationPrice;
        var isSellStopLimit = Equals(order.Side, OrderSideEnum.Sell) && Equals(order.Type, OrderTypeEnum.StopLimit) && pointModel.Bid <= order.ActivationPrice;

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
    /// <returns></returns>
    protected virtual PointModel GetRecord(IDictionary<string, StreamReader> streams, IDictionary<string, PointModel> points)
    {
      var index = string.Empty;

      foreach (var stream in streams)
      {
        points.TryGetValue(stream.Key, out PointModel point);

        if (point is null)
        {
          var input = stream.Value.ReadLine();

          if (string.IsNullOrEmpty(input) is false)
          {
            points[stream.Key] = Parse(stream.Key, input);
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
    /// Parse point
    /// </summary>
    /// <param name="name"></param>
    /// <param name="input"></param>
    /// <returns></returns>
    protected virtual PointModel Parse(string name, string input)
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
        Last = ask,
        AskSize = askSize,
        BidSize = bidSize,
        Instrument = new Instrument()
        {
          Name = name
        }
      };

      if (askSize.IsEqual(0))
      {
        response.Last = bid;
      }

      return response;
    }

    public override Task<ResponseItemModel<IList<PointModel>>> GetPoints(PointMessageModel message)
    {
      throw new NotImplementedException();
    }

    public override Task<ResponseItemModel<IList<OptionModel>>> GetOptions(OptionMessageModel message)
    {
      throw new NotImplementedException();
    }
  }
}
