using FluentValidation.Results;
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
  /// Generic market data gateway
  /// </summary>
  public interface IDataModel
  {
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

    /// <summary>
    /// Reference to the account
    /// </summary>
    IAccountModel Account { get; set; }

    /// <summary>
    /// Incoming data event
    /// </summary>
    ISubject<ITransactionMessage<IPointModel>> DataStream { get; }
  }

  /// <summary>
  /// Generic trading gateway
  /// </summary>
  public interface ITradeModel
  {
    /// <summary>
    /// Send order event
    /// </summary>
    ISubject<ITransactionMessage<ITransactionOrderModel>> OrderStream { get; }

    /// <summary>
    /// Send orders
    /// </summary>
    /// <param name="action"></param>
    /// <param name="orders"></param>
    /// <returns></returns>
    Task<IEnumerable<ITransactionOrderModel>> SendOrders(ActionEnum action, params ITransactionOrderModel[] orders);
  }

  /// <summary>
  /// Interface that defines input and output processes
  /// </summary>
  public interface IConnectorModel : IBaseModel, IDataModel, ITradeModel
  {
    /// <summary>
    /// Production or Development mode
    /// </summary>
    EnvironmentEnum Mode { get; set; }
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
    /// Reference to the account
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
      Mode = EnvironmentEnum.Sandbox;
      DataStream = new Subject<ITransactionMessage<IPointModel>>();
      OrderStream = new Subject<ITransactionMessage<ITransactionOrderModel>>();
    }

    /// <summary>
    /// Restore state and initialize
    /// </summary>
    public virtual Task Connect()
    {
      return Task.FromResult(0);
    }

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public virtual Task Disconnect()
    {
      return Task.FromResult(0);
    }

    /// <summary>
    /// Suspend execution
    /// </summary>
    public virtual Task Unsubscribe()
    {
      return Task.FromResult(0);
    }

    /// <summary>
    /// Continue execution
    /// </summary>
    public virtual Task Subscribe()
    {
      return Task.FromResult(0);
    }

    /// <summary>
    /// Send orders
    /// </summary>
    /// <param name="action"></param>
    /// <param name="orders"></param>
    /// <returns></returns>
    public virtual Task<IEnumerable<ITransactionOrderModel>> SendOrders(ActionEnum action, params ITransactionOrderModel[] orders)
    {
      return Task.FromResult(orders as IEnumerable<ITransactionOrderModel>);
    }

    /// <summary>
    /// Ensure that each series has a name and can be attached to specific area on the chart
    /// </summary>
    /// <param name="orders"></param>
    protected bool ValidateOrders(params ITransactionOrderModel[] orders)
    {
      var errors = new List<ValidationFailure>();
      var orderRules = InstanceService<TransactionOrderPriceValidator>.Instance;
      var instrumentRules = InstanceService<InstrumentCollectionsValidator>.Instance;

      foreach (var model in orders)
      {
        errors.AddRange(orderRules.Validate(model).Errors);
        errors.AddRange(instrumentRules.Validate(model.Instrument).Errors);
        errors.AddRange(model.Orders.SelectMany(o => orderRules.Validate(o).Errors));
        errors.AddRange(model.Orders.SelectMany(o => instrumentRules.Validate(o.Instrument).Errors));
      }

      foreach (var error in errors)
      {
        InstanceService<LogService>.Instance.Log.Error(error.ErrorMessage);
      }

      return errors.Any() is false;
    }

    /// <summary>
    /// Update missing values of a data point
    /// </summary>
    /// <param name="point"></param>
    protected virtual IPointModel UpdatePoints(IPointModel point)
    {
      point.Account = Account;
      point.Name = point.Instrument.Name;
      point.TimeFrame = point.Instrument.TimeFrame;

      point.Instrument.Points.Add(point);
      point.Instrument.PointGroups.Add(point, point.TimeFrame);

      var message = new TransactionMessage<IPointModel>
      {
        Action = ActionEnum.Create,
        Next = point.Instrument.PointGroups.LastOrDefault()
      };

      DataStream.OnNext(message);

      return point;
    }
  }
}
