using FluentValidation.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Terminal.Core.EnumSpace;
using Terminal.Core.MessageSpace;
using Terminal.Core.ServiceSpace;
using Terminal.Core.ValidatorSpace;

namespace Terminal.Core.ModelSpace
{
  /// <summary>
  /// Interface that defines input and output processes
  /// </summary>
  public interface IConnectorModel : IBaseModel, IDisposable
  {
    /// <summary>
    /// Production or Development mode
    /// </summary>
    EnvironmentEnum Mode { get; set; }

    /// <summary>
    /// Account
    /// </summary>
    IAccountModel Account { get; set; }

    /// <summary>
    /// Send order event
    /// </summary>
    ISubject<ITransactionMessage<ITransactionOrderModel>> OrderStream { get; }

    /// <summary>
    /// Incoming data event
    /// </summary>
    ISubject<ITransactionMessage<IPointModel>> DataStream { get; }

    /// <summary>
    /// Restore state and initialize
    /// </summary>
    Task Connect();

    /// <summary>
    /// Save state and dispose
    /// </summary>
    Task Disconnect();

    /// <summary>
    /// Continue execution
    /// </summary>
    Task Subscribe();

    /// <summary>
    /// Suspend execution
    /// </summary>
    Task Unsubscribe();
  }

  /// <summary>
  /// Implementation
  /// </summary>
  public abstract class ConnectorModel : BaseModel, IConnectorModel
  {
    /// <summary>
    /// Production or Sandbox
    /// </summary>
    public virtual EnvironmentEnum Mode { get; set; }

    /// <summary>
    /// Account
    /// </summary>
    public virtual IAccountModel Account { get; set; }

    /// <summary>
    /// Incoming data event
    /// </summary>
    public virtual ISubject<ITransactionMessage<IPointModel>> DataStream { get; }

    /// <summary>
    /// Send order event
    /// </summary>
    public virtual ISubject<ITransactionMessage<ITransactionOrderModel>> OrderStream { get; }

    /// <summary>
    /// Constructor
    /// </summary>
    public ConnectorModel()
    {
      Mode = EnvironmentEnum.Paper;
      DataStream = new Subject<ITransactionMessage<IPointModel>>();
      OrderStream = new Subject<ITransactionMessage<ITransactionOrderModel>>();
    }

    /// <summary>
    /// Restore state and initialize
    /// </summary>
    public abstract Task Connect();

    /// <summary>
    /// Continue execution
    /// </summary>
    public abstract Task Subscribe();

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public abstract Task Disconnect();

    /// <summary>
    /// Unsubscribe from data streams
    /// </summary>
    public abstract Task Unsubscribe();

    /// <summary>
    /// Dispose
    /// </summary>
    public virtual void Dispose() => Disconnect();

    /// <summary>
    /// Ensure all properties have correct values
    /// </summary>
    /// <param name="orders"></param>
    protected virtual IList<ValidationFailure> ValidateOrders(params ITransactionOrderModel[] orders)
    {
      var errors = new List<ValidationFailure>();
      var orderRules = InstanceService<TransactionOrderPriceValidator>.Instance;
      var instrumentRules = InstanceService<InstrumentCollectionValidator>.Instance;

      foreach (var order in orders)
      {
        errors.AddRange(orderRules.Validate(order).Errors);
        errors.AddRange(instrumentRules.Validate(order.Instrument).Errors);
        errors.AddRange(order.Orders.SelectMany(o => orderRules.Validate(o).Errors));
        errors.AddRange(order.Orders.SelectMany(o => instrumentRules.Validate(o.Instrument).Errors));
      }

      foreach (var error in errors)
      {
        InstanceService<LogService>.Instance.Log.Error(error.ErrorMessage);
      }

      return errors;
    }

    /// <summary>
    /// Define open price based on order
    /// </summary>
    /// <param name="nextOrder"></param>
    protected virtual IList<ITransactionOrderModel> GetOpenPrices(ITransactionOrderModel nextOrder)
    {
      var openPrice = nextOrder.Price;
      var pointModel = nextOrder.Instrument.Points.Last();

      if (openPrice is null)
      {
        switch (nextOrder.Side)
        {
          case OrderSideEnum.Buy: openPrice = pointModel.Ask; break;
          case OrderSideEnum.Sell: openPrice = pointModel.Bid; break;
        }
      }

      return new List<ITransactionOrderModel>
      {
        new TransactionOrderModel
        {
          Price = openPrice,
          Volume = nextOrder.Volume,
          Time = nextOrder.Time
        }
      };
    }

    /// <summary>
    /// Update points
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    protected virtual IPointModel UpdatePoints(IPointModel point)
    {
      var instrument = Account.Instruments[point.Name];
      var estimates = Account.ActivePositions.Select(o => o.Value.GainLossEstimate).ToList();

      point.Account = Account;
      point.Instrument = instrument;
      point.TimeFrame = instrument.TimeFrame;

      instrument.Points.Add(point);
      instrument.PointGroups.Add(point, instrument.TimeFrame);

      return point;
    }
  }
}
