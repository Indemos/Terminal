using Distribution.DomainSpace;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Terminal.Core.EnumSpace;
using Terminal.Core.ExtensionSpace;
using Terminal.Core.MessageSpace;
using Terminal.Core.ModelSpace;
using Terminal.Core.ServiceSpace;

namespace Terminal.Connector.Simulation
{
  public class Adapter : ConnectorModel, IDisposable
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
    /// Streams
    /// </summary>
    protected IDictionary<string, StreamReader> _streams;

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
      _streams = new Dictionary<string, StreamReader>();
    }

    /// <summary>
    /// Connect
    /// </summary>
    public override async Task Connect()
    {
      await Disconnect();

      Account.InitialBalance = Account.Balance;

      _streams = Account
        .Instruments
        .ToDictionary(o => o.Key, o => new StreamReader(Path.Combine(Source, o.Value.Name)));

      _streams.ForEach(o => _connections.Add(o.Value));

      await Subscribe();
    }

    /// <summary>
    /// Subscribe to data streams
    /// </summary>
    public override async Task Subscribe()
    {
      await Unsubscribe();

      var dataStream = Account
        .Instruments
        .Select(o => o.Value.Points.ItemStream)
        .Merge()
        .Subscribe(message => ProcessPendingOrders());

      var orderStream = OrderStream.Subscribe(message =>
      {
        switch (message.Action)
        {
          case ActionEnum.Create: CreateOrders(message.Next); break;
          case ActionEnum.Update: UpdateOrders(message.Next); break;
          case ActionEnum.Delete: DeleteOrders(message.Next); break;
        }
      });

      var balanceStream = Account.Positions.ItemStream.Subscribe(message =>
      {
        if (Equals(message.Action, ActionEnum.Create))
        {
          Account.Balance += message.Next.GainLoss;
        }
      });

      var span = TimeSpan.FromMilliseconds(Speed);
      var scene = InstanceService<Scene>.Instance;
      var points = new ConcurrentDictionary<string, IPointModel>();
      var interval = Observable
        .Interval(span, scene.Scheduler)
        .Subscribe(o =>
        {
          var point = GetPoint(_streams, points);

          if (point is not null)
          {
            UpdatePoints(point);
          }
        });

      _subscriptions.Add(balanceStream);
      _subscriptions.Add(orderStream);
      _subscriptions.Add(dataStream);
      _subscriptions.Add(interval);
    }

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public override Task Disconnect()
    {
      Unsubscribe();

      _connections?.ForEach(o => o.Dispose());
      _connections?.Clear();

      return Task.FromResult(0);
    }

    /// <summary>
    /// Unsubscribe from data streams
    /// </summary>
    public override Task Unsubscribe()
    {
      _subscriptions?.ForEach(o => o.Dispose());
      _subscriptions?.Clear();

      return Task.FromResult(0);
    }

    /// <summary>
    /// Create order and depending on the account, send it to the processing queue
    /// </summary>
    /// <param name="orders"></param>
    protected virtual void CreateOrders(params ITransactionOrderModel[] orders)
    {
      if (ValidateOrders(orders).Any())
      {
        return;
      }

      foreach (var nextOrder in orders)
      {
        switch (nextOrder.Type)
        {
          case OrderTypeEnum.Stop:
          case OrderTypeEnum.Limit:
          case OrderTypeEnum.StopLimit: SendPendingOrder(nextOrder); break;
          case OrderTypeEnum.Market: SendOrder(nextOrder); break;
        }
      }
    }

    /// <summary>
    /// Update orders
    /// </summary>
    /// <param name="orders"></param>
    protected virtual ITransactionOrderModel[] UpdateOrders(params ITransactionOrderModel[] orders)
    {
      foreach (var nextOrder in orders)
      {
        var order = Account.ActiveOrders[nextOrder.Id];

        if (order is not null)
        {
          var update = GetPosition(nextOrder);

          if (ValidateOrders().Any() is false)
          {
            Account.ActiveOrders[nextOrder.Id] = update;
          }
        }
      }

      return orders;
    }

    /// <summary>
    /// Recursively cancel orders
    /// </summary>
    /// <param name="orders"></param>
    protected virtual ITransactionOrderModel[] DeleteOrders(params ITransactionOrderModel[] orders)
    {
      foreach (var nextOrder in orders)
      {
        nextOrder.Status = OrderStatusEnum.Cancelled;

        if (Account.ActiveOrders.ContainsKey(nextOrder.Id))
        {
          Account.ActiveOrders.Remove(nextOrder.Id);
        }
      }

      return orders;
    }

    /// <summary>
    /// Process pending order
    /// </summary>
    /// <param name="nextOrder"></param>
    /// <returns></returns>
    protected virtual ITransactionOrderModel SendPendingOrder(ITransactionOrderModel nextOrder)
    {
      nextOrder.Status = OrderStatusEnum.Placed;

      Account.Orders.Add(nextOrder);
      Account.ActiveOrders.Add(nextOrder.Id, nextOrder);

      return nextOrder;
    }

    /// <summary>
    /// Position opening logic 
    /// </summary>
    /// <param name="nextOrder"></param>
    /// <returns></returns>
    protected virtual ITransactionPositionModel SendOrder(ITransactionOrderModel nextOrder)
    {
      var previousPosition = Account
        .ActivePositions
        .Values
        .FirstOrDefault(o => Equals(o.Instrument.Name, nextOrder.Instrument.Name));

      return previousPosition is null ?
        CreatePosition(nextOrder) :
        UpdatePosition(nextOrder, previousPosition);
    }

    /// <summary>
    /// Create position when there are no other positions
    /// </summary>
    /// <param name="nextOrder"></param>
    /// <returns></returns>
    protected virtual ITransactionPositionModel CreatePosition(ITransactionOrderModel nextOrder)
    {
      var openPrices = GetOpenPrices(nextOrder);
      var openPrice = openPrices.Last();

      nextOrder.Time = openPrice.Time;
      nextOrder.Price = openPrice.Price;
      nextOrder.Status = OrderStatusEnum.Filled;

      var nextPosition = GetPosition(nextOrder);

      nextPosition.Time = openPrice.Time;
      nextPosition.OpenPrices = openPrices;
      nextPosition.OpenPrice = nextOrder.Price;

      Account.Orders.Add(nextOrder);
      Account.ActivePositions.Add(nextPosition.Id, nextPosition);

      var message = new TransactionMessage<ITransactionOrderModel>
      {
        Action = ActionEnum.Create,
        Next = nextOrder
      };

      nextOrder.OrderStream.OnNext(message);

      return nextPosition;
    }

    /// <summary>
    /// Update position  
    /// </summary>
    /// <param name="nextOrder"></param>
    /// <param name="previousPosition"></param>
    /// <returns></returns>
    protected virtual ITransactionPositionModel UpdatePosition(ITransactionOrderModel nextOrder, ITransactionPositionModel previousPosition)
    {
      var isSameBuy = Equals(previousPosition.Side, OrderSideEnum.Buy) && Equals(nextOrder.Side, OrderSideEnum.Buy);
      var isSameSell = Equals(previousPosition.Side, OrderSideEnum.Sell) && Equals(nextOrder.Side, OrderSideEnum.Sell);
      var openPrice = GetOpenPrices(nextOrder).Last();

      nextOrder.Time = openPrice.Time;
      nextOrder.Price = openPrice.Price;
      nextOrder.Status = OrderStatusEnum.Filled;

      var nextPosition = isSameBuy || isSameSell ?
        IncreasePosition(nextOrder, previousPosition) :
        DecreasePosition(nextOrder, previousPosition);

      var message = new TransactionMessage<ITransactionOrderModel>
      {
        Action = ActionEnum.Update,
        Next = nextOrder
      };

      nextOrder.OrderStream.OnNext(message);

      return nextPosition;
    }

    /// <summary>
    /// Create position when there is a position 
    /// </summary>
    /// <param name="nextOrder"></param>
    /// <param name="previousPosition"></param>
    /// <returns></returns>
    protected virtual ITransactionPositionModel IncreasePosition(ITransactionOrderModel nextOrder, ITransactionPositionModel previousPosition)
    {
      var nextPosition = GetPosition(nextOrder);
      var openPrices = GetOpenPrices(nextOrder);
      var openPrice = openPrices.Last();

      nextPosition.Time = openPrice.Time;
      nextPosition.Volume = nextOrder.Volume + previousPosition.Volume;
      nextPosition.OpenPrices = previousPosition.OpenPrices.Concat(openPrices).ToList();
      nextPosition.OpenPrice = nextPosition.OpenPrices.Sum(o => o.Volume * o.Price) / nextPosition.OpenPrices.Sum(o => o.Volume);

      previousPosition.CloseTime = nextPosition.Time;
      previousPosition.ClosePrice = openPrice.Price;
      previousPosition.GainLoss = previousPosition.GainLossEstimate;
      previousPosition.GainLossPoints = previousPosition.GainLossPointsEstimate;

      Account.ActivePositions.Remove(previousPosition.Id);
      Account.ActivePositions.Add(nextPosition.Id, nextPosition);
      Account.Orders.Add(nextOrder);

      return nextPosition;
    }

    /// <summary>
    /// Create position when there is a position with the same transaction type 
    /// </summary>
    /// <param name="nextOrder"></param>
    /// <param name="previousPosition"></param>
    /// <returns></returns>
    protected virtual ITransactionPositionModel DecreasePosition(ITransactionOrderModel nextOrder, ITransactionPositionModel previousPosition)
    {
      var nextPosition = GetPosition(nextOrder);
      var openPrices = GetOpenPrices(nextOrder);
      var openPrice = openPrices.Last();

      nextPosition.Time = openPrice.Time;
      nextPosition.OpenPrice = openPrice.Price;
      nextPosition.OpenPrices = openPrices;
      nextPosition.Volume = Math.Abs(nextPosition.Volume.Value - previousPosition.Volume.Value);

      previousPosition.CloseTime = nextPosition.Time;
      previousPosition.ClosePrice = nextPosition.OpenPrice;
      previousPosition.GainLoss = previousPosition.GainLossEstimate;
      previousPosition.GainLossPoints = previousPosition.GainLossPointsEstimate;

      Account.ActivePositions.Remove(previousPosition.Id);
      Account.Positions.Add(previousPosition);
      Account.Orders.Add(nextOrder);

      if (nextPosition.Volume.Value.IsEqual(0) is false)
      {
        Account.ActivePositions.Add(nextPosition.Id, nextPosition);
      }

      return nextPosition;
    }

    /// <summary>
    /// Update position properties based on specified order
    /// </summary>
    /// <param name="nextOrder"></param>
    protected virtual ITransactionPositionModel GetPosition(ITransactionOrderModel nextOrder)
    {
      return new TransactionPositionModel
      {
        Id = nextOrder.Id,
        Name = nextOrder.Name,
        Description = nextOrder.Description,
        Type = nextOrder.Type,
        Volume = nextOrder.Volume,
        Side = nextOrder.Side,
        Group = nextOrder.Group,
        Price = nextOrder.Price,
        ActivationPrice = nextOrder.ActivationPrice,
        Instrument = nextOrder.Instrument,
        Orders = nextOrder.Orders
      };
    }

    /// <summary>
    /// Process pending orders
    /// </summary>
    protected virtual void ProcessPendingOrders()
    {
      var positionOrders = Account.ActivePositions.SelectMany(o => o.Value.Orders);
      var activeOrders = Account.ActiveOrders.Values.Concat(positionOrders);

      foreach (var order in activeOrders)
      {
        var pointModel = order.Instrument.Points.LastOrDefault();

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
          isExecutable = pointModel.Ask >= order.Price;
        }

        if (isSellStop || isBuyLimit)
        {
          isExecutable = pointModel.Bid <= order.Price;
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
    protected virtual IPointModel GetPoint(IDictionary<string, StreamReader> streams, IDictionary<string, IPointModel> points)
    {
      var index = string.Empty;

      foreach (var stream in streams)
      {
        points.TryGetValue(stream.Key, out IPointModel point);

        if (point is null)
        {
          var input = stream.Value.ReadLine();

          if (string.IsNullOrEmpty(input) is false)
          {
            points[stream.Key] = Parse(stream.Key, input);
          }
        }

        points.TryGetValue(index, out IPointModel min);
        points.TryGetValue(stream.Key, out IPointModel current);

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
    protected virtual IPointModel Parse(string name, string input)
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
        Name = name,
        Time = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(date),
        Ask = ask,
        Bid = bid,
        Last = ask,
        AskSize = askSize,
        BidSize = bidSize
      };

      if (askSize.IsEqual(0))
      {
        response.Last = bid;
      }

      return response;
    }
  }
}
