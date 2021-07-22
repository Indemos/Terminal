using Core.EnumSpace;
using Core.ManagerSpace;
using Core.ModelSpace;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Gateway.Simulation
{
  /// <summary>
  /// Implementation
  /// </summary>
  public class GatewayClient : GatewayModel
  {
    /// <summary>
    /// Initial simulation data
    /// </summary>
    protected IList<IDisposable> _clocks = new List<IDisposable>();

    /// <summary>
    /// Source files with quotes
    /// </summary>
    protected IList<StreamReader> _documents = new List<StreamReader>();

    /// <summary>
    /// Queue of data points synced by time
    /// </summary>
    protected ConcurrentDictionary<dynamic, IPointModel> _points = new ConcurrentDictionary<dynamic, IPointModel>();

    /// <summary>
    /// Simulation speed in milliseconds
    /// </summary>
    public virtual int Speed { get; set; } = 100;

    /// <summary>
    /// Location of the files with quotes
    /// </summary>
    public virtual string Source { get; set; } = null;

    /// <summary>
    /// Function to parse files with quotes
    /// </summary>
    public virtual Func<dynamic, IPointModel> Evaluate { get; set; } = (o) => null;

    /// <summary>
    /// Establish connection with a server
    /// </summary>
    /// <param name="docHeader"></param>
    public override Task Connect()
    {
      _documents.ForEach(o => o.Dispose());
      _documents.Clear();
      _points.Clear();

      // Orders

      OrderSenderStream
        .TakeUntil(_subscriptions)
        .Subscribe(message =>
        {
          switch (message.Action)
          {
            case ActionEnum.Create: CreateOrder(message.Next); break;
            case ActionEnum.Update: UpdateOrder(message.Next); break;
            case ActionEnum.Delete: DeleteOrder(message.Next); break;
          }
        });

      Account
        .Instruments
        .Select(o => o.Value.Points.ItemStream)
        .Merge()
        .TakeUntil(_subscriptions)
        .Subscribe(message => ProcessPendingOrders());

      // Prepare documents

      _documents = Account
        .Instruments
        .Select(instrument => new StreamReader(Path.Combine(Source, instrument.Value.Name)))
        .ToList();

      // Skip header

      _documents
        .Select(doc => doc.ReadLine())
        .ToList();

      Subscribe();

      return Task.FromResult(0);
    }

    /// <summary>
    /// Subscribe for incoming data
    /// </summary>
    public override Task Disconnect()
    {
      _subscriptions.OnNext(true);

      Unsubscribe();

      _subscriptions.OnNext(false);

      return Task.FromResult(0);
    }

    /// <summary>
    /// Subscribe for incoming data
    /// </summary>
    public override Task Subscribe()
    {
      Unsubscribe();

      var span = TimeSpan.FromMilliseconds(Speed);
      var scheduler = InstanceManager<ScheduleService>.Instance.Scheduler;
      var clock = Observable
        .Interval(span, scheduler)
        .TakeUntil(_subscriptions)
        .Subscribe(o => GeneratePoints());

      _clocks.Add(clock);

      return Task.FromResult(0);
    }

    /// <summary>
    /// Unsubscribe from incoming data
    /// </summary>
    public override Task Unsubscribe()
    {
      _clocks.ForEach(o => o.Dispose());
      _clocks.Clear();

      return Task.FromResult(0);
    }

    /// <summary>
    /// Dispose
    /// </summary>
    public override void Dispose()
    {
      Disconnect();
    }

    /// <summary>
    /// Add data point to the collection
    /// </summary>
    /// <returns></returns>
    protected virtual Task GeneratePoints()
    {
      var index = -1;

      for (var i = 0; i < _documents.Count; i++)
      {
        _points.TryGetValue(i, out IPointModel item);

        if (item == null)
        {
          var line = _documents[i].ReadLine();

          if (string.IsNullOrEmpty(line) == false)
          {
            _points[i] = Evaluate(line);
          }
        }

        _points.TryGetValue(i, out IPointModel x);
        _points.TryGetValue(index, out IPointModel y);

        var isSingle = index == -1;
        var isMin = x != null && y != null && x.Time <= y.Time;

        if (isSingle || isMin)
        {
          index = i;
        }
      }

      if (index == -1)
      {
        _clocks.ForEach(o => o.Dispose());

        return Task.FromResult(0);
      }

      var name = Account.Instruments.Keys.ElementAt(index);
      var instrument = Account.Instruments[name];

      _points[index].Bar ??= new PointBarModel();

      UpdatePointParams(_points[index], instrument);

      _points[index] = null;

      return Task.FromResult(0);
    }

    /// <summary>
    /// Create order and depending on the account, send it to the processing queue
    /// </summary>
    /// <param name="orders"></param>
    protected virtual Task<IEnumerable<ITransactionOrderModel>> CreateOrder(params ITransactionOrderModel[] orders)
    {
      if (EnsureOrderProps(orders) == false)
      {
        return Task.FromResult<IEnumerable<ITransactionOrderModel>>(null);
      }

      foreach (var nextOrder in orders)
      {
        switch (nextOrder.Type)
        {
          case TransactionTypeEnum.Buy:
          case TransactionTypeEnum.Sell:

            CreatePosition(nextOrder);
            break;

          case TransactionTypeEnum.BuyStop:
          case TransactionTypeEnum.BuyLimit:
          case TransactionTypeEnum.SellStop:
          case TransactionTypeEnum.SellLimit:

            // Process stop and limit orders

            nextOrder.Status = TransactionStatusEnum.Placed;
            Account.Orders.Add(nextOrder);
            Account.ActiveOrders.Add(nextOrder);
            break;
        }

        // Process bracket orders

        foreach (var order in nextOrder.Orders)
        {
          order.Status = TransactionStatusEnum.Placed;
          Account.ActiveOrders.Add(order);
        }
      }

      return Task.FromResult<IEnumerable<ITransactionOrderModel>>(orders);
    }

    /// <summary>
    /// Update order implementation
    /// </summary>
    /// <param name="orders"></param>
    protected virtual Task<IEnumerable<ITransactionOrderModel>> UpdateOrder(params ITransactionOrderModel[] orders)
    {
      foreach (var nextOrder in orders)
      {
        foreach (var order in Account.ActiveOrders)
        {
          if (Equals(order.Id, nextOrder.Id))
          {
            order.Type = nextOrder.Type;
            order.Size = nextOrder.Size;
            order.Price = nextOrder.Price;
            order.Orders = nextOrder.Orders;
          }
        }
      }

      return Task.FromResult<IEnumerable<ITransactionOrderModel>>(orders);
    }

    /// <summary>
    /// Delete or cancel order
    /// </summary>
    /// <param name="orders"></param>
    protected virtual Task<IEnumerable<ITransactionOrderModel>> DeleteOrder(params ITransactionOrderModel[] orders)
    {
      foreach (var nextOrder in orders)
      {
        nextOrder.Status = TransactionStatusEnum.Cancelled;
        Account.ActiveOrders.Remove(nextOrder);

        foreach (var order in nextOrder.Orders)
        {
          Account.ActiveOrders.Remove(order);
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

      return
        OpenPosition(nextOrder, previousPosition) ??
        IncreasePosition(nextOrder, previousPosition) ??
        DecreasePosition(nextOrder, previousPosition);
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
      nextOrder.Status = TransactionStatusEnum.Filled;

      var nextPosition = UpdatePositionParams(new TransactionPositionModel(), nextOrder);

      nextPosition.Time = pointModel.Time;
      nextPosition.OpenPrices = openPrices;
      nextPosition.Type = GetPositionSide(nextOrder);
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

      var nextSide = GetPositionSide(nextOrder);
      var isSameBuy = Equals(previousPosition.Type, TransactionTypeEnum.Buy) && Equals(nextSide, TransactionTypeEnum.Buy);
      var isSameSell = Equals(previousPosition.Type, TransactionTypeEnum.Sell) && Equals(nextSide, TransactionTypeEnum.Sell);

      if (isSameBuy == false && isSameSell == false)
      {
        return null;
      }

      var openPrices = GetOpenPrices(nextOrder);
      var pointModel = nextOrder.Instrument.PointGroups.LastOrDefault();

      nextOrder.Time = pointModel.Time;
      nextOrder.Price = openPrices.Last().Price;
      nextOrder.Status = TransactionStatusEnum.Filled;

      var nextPosition = UpdatePositionParams(new TransactionPositionModel(), nextOrder);

      nextPosition.Time = pointModel.Time;
      nextPosition.Price = nextOrder.Price;
      nextPosition.Type = nextSide;
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

      var nextSide = GetPositionSide(nextOrder);
      var isSameBuy = Equals(previousPosition.Type, TransactionTypeEnum.Buy) && Equals(nextSide, TransactionTypeEnum.Buy);
      var isSameSell = Equals(previousPosition.Type, TransactionTypeEnum.Sell) && Equals(nextSide, TransactionTypeEnum.Sell);

      if (isSameBuy || isSameSell)
      {
        return null;
      }

      var openPrices = GetOpenPrices(nextOrder);
      var pointModel = nextOrder.Instrument.PointGroups.LastOrDefault();

      nextOrder.Time = pointModel.Time;
      nextOrder.Price = openPrices.Last().Price;
      nextOrder.Status = TransactionStatusEnum.Filled;

      var nextPosition = UpdatePositionParams(new TransactionPositionModel(), nextOrder);

      nextPosition.Time = pointModel.Time;
      nextPosition.OpenPrices = openPrices;
      nextPosition.Type = nextSide;
      nextPosition.Price = nextPosition.OpenPrice = nextOrder.Price;
      nextPosition.Size = Math.Abs(nextPosition.Size.Value - previousPosition.Size.Value);

      previousPosition.CloseTime = nextPosition.Time;
      previousPosition.ClosePrice = nextPosition.OpenPrice;
      previousPosition.GainLoss = previousPosition.GainLossEstimate;
      previousPosition.GainLossPoints = previousPosition.GainLossPointsEstimate;

      Account.ActiveOrders.Remove(nextOrder);
      Account.ActivePositions.Remove(previousPosition);

      Account.Orders.Add(nextOrder);
      Account.Positions.Add(previousPosition);

      if (ConversionManager.Equals(nextPosition, 0.0) == false)
      {
        Account.ActivePositions.Add(nextPosition);
      }

      return nextPosition;
    }

    /// <summary>
    /// Define transaction type based on order
    /// </summary>
    /// <param name="nextOrder"></param>
    protected virtual TransactionTypeEnum GetPositionSide(ITransactionOrderModel nextOrder)
    {
      switch (nextOrder.Type)
      {
        case TransactionTypeEnum.Buy:
        case TransactionTypeEnum.BuyStop:
        case TransactionTypeEnum.BuyLimit: return TransactionTypeEnum.Buy;
        case TransactionTypeEnum.Sell:
        case TransactionTypeEnum.SellStop:
        case TransactionTypeEnum.SellLimit: return TransactionTypeEnum.Sell;
      }

      return TransactionTypeEnum.None;
    }

    /// <summary>
    /// Define open price based on order
    /// </summary>
    /// <param name="nextOrder"></param>
    protected virtual IList<ITransactionOrderModel> GetOpenPrices(ITransactionOrderModel nextOrder)
    {
      var openPrice = nextOrder.Price;
      var pointModel = nextOrder.Instrument.PointGroups.LastOrDefault();

      if (ConversionManager.Equals(openPrice ?? 0.0, 0.0))
      {
        openPrice = Equals(GetPositionSide(nextOrder), TransactionTypeEnum.Buy) ? pointModel.Ask : pointModel.Bid;
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
      foreach (var order in Account.ActiveOrders)
      {
        var executable = false;
        var pointModel = order.Instrument.Points.LastOrDefault();

        if (pointModel != null)
        {
          switch (order.Type)
          {
            case TransactionTypeEnum.BuyStop:
            case TransactionTypeEnum.SellLimit: executable = pointModel.Ask >= order.Price; break;
            case TransactionTypeEnum.SellStop:
            case TransactionTypeEnum.BuyLimit: executable = pointModel.Bid <= order.Price; break;
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
