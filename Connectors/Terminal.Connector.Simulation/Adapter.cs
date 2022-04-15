using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Terminal.Core.EnumSpace;
using Terminal.Core.ExtensionSpace;
using Terminal.Core.ModelSpace;
using Terminal.Core.ServiceSpace;

namespace Terminal.Connector.Simulation
{
  /// <summary>
  /// Implementation
  /// </summary>
  public class Adapter : ConnectorModel, IDisposable
  {
    /// <summary>
    /// Disposable connections
    /// </summary>
    protected IList<IDisposable> _connections = null;

    /// <summary>
    /// Disposable subscriptions
    /// </summary>
    protected IList<IDisposable> _subscriptions = null;

    /// <summary>
    /// Queue of data points synced by time
    /// </summary>
    protected IDictionary<string, IPointModel> _points = null;

    /// <summary>
    /// Source files with quotes
    /// </summary>
    protected IDictionary<string, StreamReader> _streams = null;

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
      _connections = new List<IDisposable>();
      _subscriptions = new List<IDisposable>();
      _points = new Dictionary<string, IPointModel>();
      _streams = new Dictionary<string, StreamReader>();

      Speed = 100;
    }

    /// <summary>
    /// Establish connection with a server
    /// </summary>
    /// <param name="docHeader"></param>
    public override Task Connect()
    {
      Disconnect();

      _streams = Account
        .Instruments
        .Select(instrument => KeyValuePair.Create(instrument.Key, new StreamReader(Path.Combine(Source, instrument.Value.Name))))
        .ToDictionary(o => o.Key, o => o.Value);

      Subscribe();

      return Task.FromResult(0);
    }

    /// <summary>
    /// Subscribe for incoming data
    /// </summary>
    public override Task Disconnect()
    {
      Unsubscribe();

      _streams?.ForEach(o => o.Value.Dispose());
      _connections?.ForEach(o => o.Dispose());

      _connections?.Clear();
      _streams?.Clear();
      _points?.Clear();

      return Task.FromResult(0);
    }

    /// <summary>
    /// Subscribe for incoming data
    /// </summary>
    public override Task Subscribe()
    {
      Unsubscribe();

      var orderStream = OrderStream.Subscribe(message =>
      {
        switch (message.Action)
        {
          case ActionEnum.Create: CreateOrders(message.Next); break;
          case ActionEnum.Update: UpdateOrders(message.Next); break;
          case ActionEnum.Delete: DeleteOrders(message.Next); break;
        }
      });

      var pointStream = Account
        .Instruments
        .Select(o => o.Value.PointGroups.ItemStream)
        .Merge()
        .Subscribe(message => ProcessPendingOrders());

      var span = TimeSpan.FromMilliseconds(Speed);
      var interval = Observable
        .Interval(span, InstanceService<ScheduleService>.Instance.Scheduler)
        .Subscribe(o => GeneratePoints());

      _subscriptions.Add(orderStream);
      _subscriptions.Add(pointStream);
      _subscriptions.Add(interval);

      return Task.FromResult(0);
    }

    /// <summary>
    /// Unsubscribe from incoming data
    /// </summary>
    public override Task Unsubscribe()
    {
      _subscriptions?.ForEach(o => o.Dispose());
      _subscriptions?.Clear();

      return Task.FromResult(0);
    }

    /// <summary>
    /// Dispose
    /// </summary>
    public virtual void Dispose()
    {
      Disconnect();
    }

    /// <summary>
    /// Parse inputs
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    protected virtual IPointModel Parse(string input)
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
        BidSize = bidSize
      };

      if (askSize.IsEqual(0))
      {
        response.Last = bid;
      }

      return response;
    }

    /// <summary>
    /// Add data point to the collection
    /// </summary>
    /// <returns></returns>
    protected virtual void GeneratePoints()
    {
      var index = string.Empty;

      foreach (var stream in _streams)
      {
        _points.TryGetValue(stream.Key, out IPointModel point);

        if (point is null)
        {
          var input = stream.Value.ReadLine();

          if (string.IsNullOrEmpty(input) is false)
          {
            _points[stream.Key] = Parse(input);
          }
        }

        _points.TryGetValue(index, out IPointModel min);
        _points.TryGetValue(stream.Key, out IPointModel current);

        var isOne = string.IsNullOrEmpty(index);
        var isMin = current is not null && min is not null && current.Time <= min.Time;

        if (isOne || isMin)
        {
          index = stream.Key;
        }
      }

      if (string.IsNullOrEmpty(index))
      {
        Unsubscribe();
        return;
      }

      _points[index].Instrument = Account.Instruments[index];

      UpdatePointProps(_points[index]);

      _points[index] = null;
    }

    /// <summary>
    /// Create order and depending on the account, send it to the processing queue
    /// </summary>
    /// <param name="orders"></param>
    public override Task<IEnumerable<ITransactionOrderModel>> CreateOrders(params ITransactionOrderModel[] orders)
    {
      if (EnsureOrderProps(orders) == false)
      {
        return Task.FromResult<IEnumerable<ITransactionOrderModel>>(null);
      }

      foreach (var nextOrder in orders)
      {
        switch (nextOrder.Category)
        {
          case OrderCategoryEnum.Market:

            CreatePosition(nextOrder);
            break;

          case OrderCategoryEnum.Stop:
          case OrderCategoryEnum.Limit:
          case OrderCategoryEnum.StopLimit:

            // Track only independent orders without parent

            if (nextOrder.Container == null)
            {
              nextOrder.Status = OrderStatusEnum.Placed;
              Account.Orders.Add(nextOrder);
              Account.ActiveOrders.Add(nextOrder);
            }

            break;
        }
      }

      return Task.FromResult<IEnumerable<ITransactionOrderModel>>(orders);
    }

    /// <summary>
    /// Update order implementation
    /// </summary>
    /// <param name="orders"></param>
    public override Task<IEnumerable<ITransactionOrderModel>> UpdateOrders(params ITransactionOrderModel[] orders)
    {
      foreach (var nextOrder in orders)
      {
        foreach (var order in Account.ActiveOrders)
        {
          if (Equals(order.Id, nextOrder.Id))
          {
            order.Category = nextOrder.Category;
            order.Size = nextOrder.Size;
            order.Price = nextOrder.Price;
            order.Orders = nextOrder.Orders;
          }
        }
      }

      return Task.FromResult<IEnumerable<ITransactionOrderModel>>(orders);
    }

    /// <summary>
    /// Recursively cancel orders
    /// </summary>
    /// <param name="orders"></param>
    public override Task<IEnumerable<ITransactionOrderModel>> DeleteOrders(params ITransactionOrderModel[] orders)
    {
      foreach (var nextOrder in orders)
      {
        nextOrder.Status = OrderStatusEnum.Cancelled;

        Account.ActiveOrders.Remove(nextOrder);

        if (nextOrder.Orders.Any())
        {
          DeleteOrders(nextOrder.Orders.ToArray());
        }
      }

      return Task.FromResult<IEnumerable<ITransactionOrderModel>>(orders);
    }

    /// <summary>
    /// Position opening logic 
    /// </summary>
    /// <param name="nextOrder"></param>
    /// <returns></returns>
    protected virtual ITransactionPositionModel CreatePosition(ITransactionOrderModel nextOrder)
    {
      var previousPosition = Account
        .ActivePositions
        .FirstOrDefault(o => Equals(o.Instrument.Name, nextOrder.Instrument.Name));

      var response =
        OpenPosition(nextOrder, previousPosition) ??
        IncreasePosition(nextOrder, previousPosition) ??
        DecreasePosition(nextOrder, previousPosition);

      // Process bracket orders

      var pointModel = nextOrder
        .Instrument
        .PointGroups
        .LastOrDefault();

      foreach (var order in nextOrder.Orders)
      {
        order.Time = pointModel?.Time;
        order.Status = OrderStatusEnum.Placed;
        Account.ActiveOrders.Add(order);
      }

      return response;
    }

    /// <summary>
    /// Create position when there are no other positions
    /// </summary>
    /// <param name="nextOrder"></param>
    /// <param name="previousPosition"></param>
    /// <returns></returns>
    protected virtual ITransactionPositionModel OpenPosition(ITransactionOrderModel nextOrder, ITransactionPositionModel previousPosition)
    {
      if (previousPosition != null)
      {
        return null;
      }

      var openPrices = GetOpenPrices(nextOrder);
      var pointModel = nextOrder.Instrument.PointGroups.LastOrDefault();

      nextOrder.Time = pointModel.Time;
      nextOrder.Price = openPrices.Last().Price;
      nextOrder.Status = OrderStatusEnum.Filled;

      var nextPosition = UpdatePositionProps(new TransactionPositionModel(), nextOrder);

      nextPosition.Time = pointModel.Time;
      nextPosition.OpenPrices = openPrices;
      nextPosition.Price = nextPosition.OpenPrice = nextOrder.Price;

      Account.Orders.Add(nextOrder);
      Account.ActiveOrders.Remove(nextOrder);
      Account.ActivePositions.Add(nextPosition);

      return nextPosition;
    }

    /// <summary>
    /// Create position when there is a position with the same transaction type 
    /// </summary>
    /// <param name="nextOrder"></param>
    /// <param name="previousPosition"></param>
    /// <returns></returns>
    protected virtual ITransactionPositionModel IncreasePosition(ITransactionOrderModel nextOrder, ITransactionPositionModel previousPosition)
    {
      if (previousPosition == null)
      {
        return null;
      }

      var isSameBuy = Equals(previousPosition.Side, OrderSideEnum.Buy) && Equals(nextOrder.Side, OrderSideEnum.Buy);
      var isSameSell = Equals(previousPosition.Side, OrderSideEnum.Sell) && Equals(nextOrder.Side, OrderSideEnum.Sell);

      if (isSameBuy == false && isSameSell == false)
      {
        return null;
      }

      var openPrices = GetOpenPrices(nextOrder);
      var pointModel = nextOrder.Instrument.PointGroups.LastOrDefault();

      nextOrder.Time = pointModel.Time;
      nextOrder.Price = openPrices.Last().Price;
      nextOrder.Status = OrderStatusEnum.Filled;

      var nextPosition = UpdatePositionProps(new TransactionPositionModel(), nextOrder);

      nextPosition.Time = pointModel.Time;
      nextPosition.Price = nextOrder.Price;
      nextPosition.Size = nextOrder.Size + previousPosition.Size;
      nextPosition.OpenPrices = previousPosition.OpenPrices.Concat(openPrices).ToList();
      nextPosition.OpenPrice = nextPosition.OpenPrices.Sum(o => o.Size * o.Price) / nextPosition.OpenPrices.Sum(o => o.Size);

      previousPosition.CloseTime = nextPosition.Time;
      previousPosition.ClosePrice = nextPosition.OpenPrice;
      previousPosition.GainLoss = previousPosition.GainLossEstimate;
      previousPosition.GainLossPoints = previousPosition.GainLossPointsEstimate;

      Account.ActiveOrders.Remove(nextOrder);
      Account.ActivePositions.Remove(previousPosition);

      Account.Orders.Add(nextOrder);
      Account.Positions.Add(previousPosition);
      Account.ActivePositions.Add(nextPosition);

      return previousPosition;
    }

    /// <summary>
    /// Create position when there is a position with the same transaction type 
    /// </summary>
    /// <param name="nextOrder"></param>
    /// <param name="previousPosition"></param>
    /// <returns></returns>
    protected virtual ITransactionPositionModel DecreasePosition(ITransactionOrderModel nextOrder, ITransactionPositionModel previousPosition)
    {
      if (previousPosition == null)
      {
        return null;
      }

      var isSameBuy = Equals(previousPosition.Side, OrderSideEnum.Buy) && Equals(nextOrder.Side, OrderSideEnum.Buy);
      var isSameSell = Equals(previousPosition.Side, OrderSideEnum.Sell) && Equals(nextOrder.Side, OrderSideEnum.Sell);

      if (isSameBuy || isSameSell)
      {
        return null;
      }

      var openPrices = GetOpenPrices(nextOrder);
      var pointModel = nextOrder.Instrument.PointGroups.LastOrDefault();

      nextOrder.Time = pointModel.Time;
      nextOrder.Price = openPrices.Last().Price;
      nextOrder.Status = OrderStatusEnum.Filled;

      var nextPosition = UpdatePositionProps(new TransactionPositionModel(), nextOrder);

      nextPosition.Time = pointModel.Time;
      nextPosition.OpenPrices = openPrices;
      nextPosition.Price = nextPosition.OpenPrice = nextOrder.Price;
      nextPosition.Size = Math.Abs(nextPosition.Size.Value - previousPosition.Size.Value);

      previousPosition.CloseTime = nextPosition.Time;
      previousPosition.ClosePrice = nextPosition.OpenPrice;
      previousPosition.GainLoss = previousPosition.GainLossEstimate;
      previousPosition.GainLossPoints = previousPosition.GainLossPointsEstimate;

      Account.Balance += previousPosition.GainLoss;
      Account.ActiveOrders.Remove(nextOrder);
      Account.ActivePositions.Remove(previousPosition);

      DeleteOrders(previousPosition.Orders.ToArray());

      Account.Orders.Add(nextOrder);
      Account.Positions.Add(previousPosition);

      if (nextPosition.Size.Equals(0) == false)
      {
        Account.ActivePositions.Add(nextPosition);
      }

      return nextPosition;
    }

    /// <summary>
    /// Update position properties based on specified order
    /// </summary>
    /// <param name="position"></param>
    /// <param name="order"></param>
    protected virtual ITransactionPositionModel UpdatePositionProps(ITransactionPositionModel position, ITransactionOrderModel order)
    {
      position.Id = order.Id;
      position.Name = order.Name;
      position.Description = order.Description;
      position.Category = order.Category;
      position.Size = order.Size;
      position.Side = order.Side;
      position.Group = order.Group;
      position.Price = order.Price;
      position.OpenPrice = order.Price;
      position.Instrument = order.Instrument;
      position.Orders = order.Orders;
      position.Time = order.Time;

      return position;
    }

    /// <summary>
    /// Define open price based on order
    /// </summary>
    /// <param name="nextOrder"></param>
    protected virtual IList<ITransactionOrderModel> GetOpenPrices(ITransactionOrderModel nextOrder)
    {
      var openPrice = nextOrder.Price;
      var pointModel = nextOrder.Instrument.PointGroups.LastOrDefault();

      if (openPrice.Equals(0.0))
      {
        openPrice = Equals(nextOrder.Side, OrderSideEnum.Buy) ? pointModel.Ask : pointModel.Bid;
      }

      return new List<ITransactionOrderModel>
      {
        new TransactionOrderModel
        {
          Price = openPrice,
          Size = nextOrder.Size,
          Time = pointModel.Time
        }
      };
    }

    /// <summary>
    /// Process pending orders implementation
    /// </summary>
    protected virtual void ProcessPendingOrders()
    {
      for (var i = 0; i < Account.ActiveOrders.Count; i++)
      {
        var order = Account.ActiveOrders[i];
        var pointModel = order.Instrument.PointGroups.LastOrDefault();

        if (pointModel != null)
        {
          var executable = false;
          var isBuyStop = Equals(order.Side, OrderSideEnum.Buy) && Equals(order.Category, OrderCategoryEnum.Stop);
          var isSellStop = Equals(order.Side, OrderSideEnum.Sell) && Equals(order.Category, OrderCategoryEnum.Stop);
          var isBuyLimit = Equals(order.Side, OrderSideEnum.Buy) && Equals(order.Category, OrderCategoryEnum.Limit);
          var isSellLimit = Equals(order.Side, OrderSideEnum.Sell) && Equals(order.Category, OrderCategoryEnum.Limit);

          if (isBuyStop || isSellLimit)
          {
            executable = pointModel.Ask >= order.Price;
          }

          if (isSellStop || isBuyLimit)
          {
            executable = pointModel.Bid <= order.Price;
          }

          if (executable)
          {
            CreatePosition(order);
          }
        }
      }
    }
  }
}
