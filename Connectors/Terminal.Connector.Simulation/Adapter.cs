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
    /// Streams
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
    /// Subscribe for incoming data
    /// </summary>
    public override Task Disconnect()
    {
      Unsubscribe();

      _connections?.ForEach(o => o.Dispose());
      _connections?.Clear();

      return Task.FromResult(0);
    }

    /// <summary>
    /// Subscribe for incoming data
    /// </summary>
    public override async Task Subscribe()
    {
      await Unsubscribe();

      var dataStream = Account
        .Instruments
        .Select(o => o.Value.Points.ItemStream)
        .Merge()
        .Subscribe(async message => await UpdatePendingOrders());

      var orderStream = OrderStream.Subscribe(async message =>
      {
        switch (message.Action)
        {
          case ActionEnum.Create: await CreateOrders(message.Next); break;
          case ActionEnum.Update: await UpdateOrders(message.Next); break;
          case ActionEnum.Delete: await DeleteOrders(message.Next); break;
        }
      });

      var balanceStream = Account.Positions.ItemStream.Subscribe(message =>
      {
        Account.Balance += message.Next.GainLoss;
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
    public override void Dispose() => Disconnect();

    /// <summary>
    /// Create order and depending on the account, send it to the processing queue
    /// </summary>
    /// <param name="orders"></param>
    protected virtual async Task<IList<ITransactionOrderModel>> CreateOrders(params ITransactionOrderModel[] orders)
    {
      if (ValidateOrders(orders) is false)
      {
        return null;
      }

      foreach (var nextOrder in orders)
      {
        switch (nextOrder.Category)
        {
          case OrderCategoryEnum.Market:

            await CreatePosition(nextOrder);
            break;

          case OrderCategoryEnum.Stop:
          case OrderCategoryEnum.Limit:
          case OrderCategoryEnum.StopLimit:

            // Track only independent orders without parent

            if (nextOrder.Container is null)
            {
              nextOrder.Status = OrderStatusEnum.Placed;

              Account.Orders.Add(nextOrder);
              Account.ActiveOrders.Add(nextOrder.Id, nextOrder);
            }

            break;
        }
      }

      return orders;
    }

    /// <summary>
    /// Update order implementation
    /// </summary>
    /// <param name="orders"></param>
    protected virtual Task<ITransactionOrderModel[]> UpdateOrders(params ITransactionOrderModel[] orders)
    {
      foreach (var nextOrder in orders)
      {
        var order = Account.ActiveOrders[nextOrder.Id];

        if (order is not null)
        {
          order.Size = nextOrder.Size;
          order.Price = nextOrder.Price;
          order.Orders = nextOrder.Orders;
          order.Category = nextOrder.Category;
        }
      }

      return Task.FromResult(orders);
    }

    /// <summary>
    /// Recursively cancel orders
    /// </summary>
    /// <param name="orders"></param>
    protected virtual async Task<ITransactionOrderModel[]> DeleteOrders(params ITransactionOrderModel[] orders)
    {
      foreach (var nextOrder in orders)
      {
        nextOrder.Status = OrderStatusEnum.Cancelled;

        if (Account.ActiveOrders.ContainsKey(nextOrder.Id))
        {
          Account.ActiveOrders.Remove(nextOrder.Id);
        }

        if (nextOrder.Orders.Any())
        {
          await DeleteOrders(nextOrder.Orders.ToArray());
        }
      }

      return orders;
    }

    /// <summary>
    /// Position opening logic 
    /// </summary>
    /// <param name="nextOrder"></param>
    /// <returns></returns>
    protected virtual async Task<ITransactionPositionModel> CreatePosition(ITransactionOrderModel nextOrder)
    {
      var previousPosition = Account
        .ActivePositions
        .Values
        .FirstOrDefault(o => Equals(o.Instrument.Name, nextOrder.Instrument.Name));

      ITransactionPositionModel response = null;

      if (previousPosition is null)
      {
        response = await OpenPosition(nextOrder, previousPosition);
      }
      else
      {
        var isSameBuy = Equals(previousPosition.Side, OrderSideEnum.Buy) && Equals(nextOrder.Side, OrderSideEnum.Buy);
        var isSameSell = Equals(previousPosition.Side, OrderSideEnum.Sell) && Equals(nextOrder.Side, OrderSideEnum.Sell);

        response = isSameBuy || isSameSell ?
          await IncreasePosition(nextOrder, previousPosition) : 
          await DecreasePosition(nextOrder, previousPosition); 
      }

      // Process bracket orders

      var pointModel = nextOrder
        .Instrument
        .PointGroups
        .LastOrDefault();

      foreach (var order in nextOrder.Orders)
      {
        order.Time = pointModel.Time;
        order.Status = OrderStatusEnum.Placed;
      }

      return response;
    }

    /// <summary>
    /// Create position when there are no other positions
    /// </summary>
    /// <param name="nextOrder"></param>
    /// <param name="previousPosition"></param>
    /// <returns></returns>
    protected virtual Task<ITransactionPositionModel> OpenPosition(ITransactionOrderModel nextOrder, ITransactionPositionModel previousPosition)
    {
      var openPrices = GetOpenPrices(nextOrder);
      var pointModel = nextOrder.Instrument.PointGroups.LastOrDefault();

      nextOrder.Time = pointModel.Time;
      nextOrder.Price = openPrices.Last().Price;
      nextOrder.Status = OrderStatusEnum.Filled;

      var nextPosition = GetPosition(new TransactionPositionModel(), nextOrder);

      nextPosition.Time = pointModel.Time;
      nextPosition.OpenPrices = openPrices;
      nextPosition.Price = nextPosition.OpenPrice = nextOrder.Price;

      Account.Orders.Add(nextOrder);
      Account.ActiveOrders.Remove(nextOrder.Id);
      Account.ActivePositions.Add(nextPosition.Id, nextPosition);

      return Task.FromResult(nextPosition);
    }

    /// <summary>
    /// Create position when there is a position with the same transaction type 
    /// </summary>
    /// <param name="nextOrder"></param>
    /// <param name="previousPosition"></param>
    /// <returns></returns>
    protected virtual async Task<ITransactionPositionModel> IncreasePosition(ITransactionOrderModel nextOrder, ITransactionPositionModel previousPosition)
    {
      var openPrices = GetOpenPrices(nextOrder);
      var pointModel = nextOrder.Instrument.PointGroups.LastOrDefault();

      nextOrder.Time = pointModel.Time;
      nextOrder.Price = openPrices.Last().Price;
      nextOrder.Status = OrderStatusEnum.Filled;

      var nextPosition = GetPosition(new TransactionPositionModel(), nextOrder);

      nextPosition.Time = pointModel.Time;
      nextPosition.Price = nextOrder.Price;
      nextPosition.Size = nextOrder.Size + previousPosition.Size;
      nextPosition.OpenPrices = previousPosition.OpenPrices.Concat(openPrices).ToList();
      nextPosition.OpenPrice = nextPosition.OpenPrices.Sum(o => o.Size * o.Price) / nextPosition.OpenPrices.Sum(o => o.Size);

      previousPosition.CloseTime = nextPosition.Time;
      previousPosition.ClosePrice = nextPosition.OpenPrice;
      previousPosition.GainLoss = previousPosition.GainLossEstimate;
      previousPosition.GainLossPoints = previousPosition.GainLossPointsEstimate;

      Account.ActiveOrders.Remove(nextOrder.Id);
      Account.ActivePositions.Remove(previousPosition.Id);

      await DeleteOrders(nextOrder.Orders.ToArray());
      await DeleteOrders(previousPosition.Orders.ToArray());

      Account.Orders.Add(nextOrder);
      Account.Positions.Add(previousPosition);
      Account.ActivePositions.Add(nextPosition.Id, nextPosition);

      return previousPosition;
    }

    /// <summary>
    /// Create position when there is a position with the same transaction type 
    /// </summary>
    /// <param name="nextOrder"></param>
    /// <param name="previousPosition"></param>
    /// <returns></returns>
    protected virtual async Task<ITransactionPositionModel> DecreasePosition(ITransactionOrderModel nextOrder, ITransactionPositionModel previousPosition)
    {
      var openPrices = GetOpenPrices(nextOrder);
      var pointModel = nextOrder.Instrument.PointGroups.LastOrDefault();

      nextOrder.Time = pointModel.Time;
      nextOrder.Price = openPrices.Last().Price;
      nextOrder.Status = OrderStatusEnum.Filled;

      var nextPosition = GetPosition(new TransactionPositionModel(), nextOrder);

      nextPosition.Time = pointModel.Time;
      nextPosition.OpenPrices = openPrices;
      nextPosition.Price = nextPosition.OpenPrice = nextOrder.Price;
      nextPosition.Size = Math.Abs(nextPosition.Size.Value - previousPosition.Size.Value);

      previousPosition.CloseTime = nextPosition.Time;
      previousPosition.ClosePrice = nextPosition.OpenPrice;
      previousPosition.GainLoss = previousPosition.GainLossEstimate;
      previousPosition.GainLossPoints = previousPosition.GainLossPointsEstimate;

      Account.ActiveOrders.Remove(nextOrder.Id);
      Account.ActivePositions.Remove(previousPosition.Id);

      await DeleteOrders(nextOrder.Orders.ToArray());
      await DeleteOrders(previousPosition.Orders.ToArray());

      Account.Orders.Add(nextOrder);
      Account.Positions.Add(previousPosition);

      if (nextPosition.Size.Value.IsEqual(0) is false)
      {
        Account.ActivePositions.Add(nextPosition.Id, nextPosition);
      }

      return nextPosition;
    }

    /// <summary>
    /// Update position properties based on specified order
    /// </summary>
    /// <param name="nextPosition"></param>
    /// <param name="nextOrder"></param>
    protected virtual ITransactionPositionModel GetPosition(ITransactionPositionModel nextPosition, ITransactionOrderModel nextOrder)
    {
      nextPosition.Id = nextOrder.Id;
      nextPosition.Name = nextOrder.Name;
      nextPosition.Description = nextOrder.Description;
      nextPosition.Category = nextOrder.Category;
      nextPosition.Size = nextOrder.Size;
      nextPosition.Side = nextOrder.Side;
      nextPosition.Group = nextOrder.Group;
      nextPosition.Price = nextOrder.Price;
      nextPosition.OpenPrice = nextOrder.Price;
      nextPosition.Instrument = nextOrder.Instrument;
      nextPosition.Orders = nextOrder.Orders;
      nextPosition.Time = nextOrder.Time;

      return nextPosition;
    }

    /// <summary>
    /// Process pending orders
    /// </summary>
    protected virtual async Task UpdatePendingOrders()
    {
      foreach (var orderItem in Account.ActiveOrders)
      {
        var order = orderItem.Value;
        var pointModel = order.Instrument.PointGroups.LastOrDefault();

        if (pointModel is not null)
        {
          var isExecutable = false;
          var isBuyStop = Equals(order.Side, OrderSideEnum.Buy) && Equals(order.Category, OrderCategoryEnum.Stop);
          var isSellStop = Equals(order.Side, OrderSideEnum.Sell) && Equals(order.Category, OrderCategoryEnum.Stop);
          var isBuyLimit = Equals(order.Side, OrderSideEnum.Buy) && Equals(order.Category, OrderCategoryEnum.Limit);
          var isSellLimit = Equals(order.Side, OrderSideEnum.Sell) && Equals(order.Category, OrderCategoryEnum.Limit);

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
            await CreatePosition(order);
          }
        }
      }
    }
  }
}
