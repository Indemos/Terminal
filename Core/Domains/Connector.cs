using FluentValidation.Results;
using Mapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Core.Enums;
using Terminal.Core.Models;
using Terminal.Core.Services;
using Terminal.Core.Validators;

namespace Terminal.Core.Domains
{
  public interface IConnector : IDisposable
  {
    /// <summary>
    /// Production or Development mode
    /// </summary>
    EnvironmentEnum Mode { get; set; }

    /// <summary>
    /// Account
    /// </summary>
    IAccount Account { get; set; }

    /// <summary>
    /// Incoming data event
    /// </summary>
    Action<StateModel<PointModel>> DataStream { get; set; }

    /// <summary>
    /// Send order event
    /// </summary>
    Action<StateModel<OrderModel>> OrderStream { get; set; }

    /// <summary>
    /// Restore state and initialize
    /// </summary>
    Task<IList<ErrorModel>> Connect();

    /// <summary>
    /// Save state and dispose
    /// </summary>
    Task<IList<ErrorModel>> Disconnect();

    /// <summary>
    /// Continue execution
    /// </summary>
    Task<IList<ErrorModel>> Subscribe();

    /// <summary>
    /// Suspend execution
    /// </summary>
    Task<IList<ErrorModel>> Unsubscribe();

    /// <summary>
    /// Get quote
    /// </summary>
    /// <param name="message"></param>
    Task<ResponseItemModel<PointModel>> GetPoint(PointMessageModel message);

    /// <summary>
    /// Get quotes history
    /// </summary>
    /// <param name="message"></param>
    Task<ResponseItemModel<IList<PointModel>>> GetPoints(PointMessageModel message);

    /// <summary>
    /// Get option chains
    /// </summary>
    /// <param name="message"></param>
    Task<ResponseItemModel<IList<OptionModel>>> GetOptions(OptionMessageModel message);

    /// <summary>
    /// Send new orders
    /// </summary>
    /// <param name="orders"></param>
    Task<ResponseModel<OrderModel>> CreateOrders(params OrderModel[] orders);

    /// <summary>
    /// Update orders
    /// </summary>
    /// <param name="orders"></param>
    Task<ResponseModel<OrderModel>> UpdateOrders(params OrderModel[] orders);

    /// <summary>
    /// Cancel orders
    /// </summary>
    /// <param name="orders"></param>
    Task<ResponseModel<OrderModel>> DeleteOrders(params OrderModel[] orders);
  }

  /// <summary>
  /// Implementation
  /// </summary>
  public abstract class Connector : IConnector
  {
    /// <summary>
    /// Production or Sandbox
    /// </summary>
    public virtual EnvironmentEnum Mode { get; set; }

    /// <summary>
    /// Account
    /// </summary>
    public virtual IAccount Account { get; set; }

    /// <summary>
    /// Incoming data event
    /// </summary>
    public virtual Action<StateModel<PointModel>> DataStream { get; set; }

    /// <summary>
    /// Send order event
    /// </summary>
    public virtual Action<StateModel<OrderModel>> OrderStream { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public Connector()
    {
      Mode = EnvironmentEnum.Paper;

      DataStream = o => { };
      OrderStream = o => { };
    }

    /// <summary>
    /// Restore state and initialize
    /// </summary>
    public abstract Task<IList<ErrorModel>> Connect();

    /// <summary>
    /// Continue execution
    /// </summary>
    public abstract Task<IList<ErrorModel>> Subscribe();

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public abstract Task<IList<ErrorModel>> Disconnect();

    /// <summary>
    /// Unsubscribe from data streams
    /// </summary>
    public abstract Task<IList<ErrorModel>> Unsubscribe();

    /// <summary>
    /// Get quote
    /// </summary>
    /// <param name="message"></param>
    public abstract Task<ResponseItemModel<PointModel>> GetPoint(PointMessageModel message);

    /// <summary>
    /// Get quotes history
    /// </summary>
    /// <param name="message"></param>
    public abstract Task<ResponseItemModel<IList<PointModel>>> GetPoints(PointMessageModel message);

    /// <summary>
    /// Get option chains
    /// </summary>
    /// <param name="message"></param>
    public abstract Task<ResponseItemModel<IList<OptionModel>>> GetOptions(OptionMessageModel message);

    /// <summary>
    /// Send new orders
    /// </summary>
    /// <param name="orders"></param>
    public abstract Task<ResponseModel<OrderModel>> CreateOrders(params OrderModel[] orders);

    /// <summary>
    /// Update orders
    /// </summary>
    /// <param name="orders"></param>
    public abstract Task<ResponseModel<OrderModel>> UpdateOrders(params OrderModel[] orders);

    /// <summary>
    /// Cancel orders
    /// </summary>
    /// <param name="orders"></param>
    public abstract Task<ResponseModel<OrderModel>> DeleteOrders(params OrderModel[] orders);

    /// <summary>
    /// Dispose
    /// </summary>
    public virtual void Dispose() => Disconnect();

    /// <summary>
    /// Set missing order properties
    /// </summary>
    /// <param name="orders"></param>
    protected virtual IList<OrderModel> CorrectOrders(params OrderModel[] orders)
    {
      foreach (var nextOrder in orders)
      {
        nextOrder.Type ??= OrderTypeEnum.Market;
        nextOrder.TimeSpan ??= OrderTimeSpanEnum.GTC;
        nextOrder.Transaction ??= new TransactionModel();
        nextOrder.Transaction.Time ??= DateTime.Now;
        nextOrder.Transaction.Price ??= GetOpenPrice(nextOrder);
        nextOrder.Transaction.Status ??= OrderStatusEnum.None;
        nextOrder.Transaction.Operation ??= OperationEnum.In;
      }

      return orders;
    }

    /// <summary>
    /// Ensure all properties have correct values
    /// </summary>
    /// <param name="orders"></param>
    protected virtual ResponseModel<OrderModel> ValidateOrders(params OrderModel[] orders)
    {
      var map = Mapper<ValidationFailure, ErrorModel>.Map;
      var orderRules = InstanceService<OrderPriceValidator>.Instance;
      var response = new ResponseModel<OrderModel>();

      foreach (var order in orders)
      {
        var errors = new List<ErrorModel>();

        errors.AddRange(orderRules.Validate(order).Errors.Select(o => map(o, new ErrorModel())));
        errors.AddRange(order.Orders.SelectMany(o => orderRules.Validate(o).Errors.Select(o => map(o, new ErrorModel()))));

        response.Count += errors.Count;
        response.Items.Add(new ResponseItemModel<OrderModel>
        {
          Data = order,
          Errors = errors
        });
      }

      return response;
    }

    /// <summary>
    /// Define open price based on order
    /// </summary>
    /// <param name="nextOrder"></param>
    protected virtual double? GetOpenPrice(OrderModel nextOrder)
    {
      var pointModel = nextOrder?.Transaction?.Instrument?.Points?.LastOrDefault();

      if (pointModel is not null)
      {
        switch (nextOrder?.Side)
        {
          case OrderSideEnum.Buy: return pointModel.Ask;
          case OrderSideEnum.Sell: return pointModel.Bid;
        }
      }

      return null;
    }

    /// <summary>
    /// Update points
    /// </summary>
    /// <param name="point"></param>
    protected virtual IList<IAccount> CorrectAccounts(params IAccount[] accounts)
    {
      foreach (var account in accounts)
      {
        account.InitialBalance = account.Balance;
      }

      return accounts;
    }

    /// <summary>
    /// Update points
    /// </summary>
    /// <param name="point"></param>
    protected virtual IList<PointModel> CorrectPoints(params PointModel[] points)
    {
      foreach (var point in points)
      {
        var instrument = Account.Instruments[point.Instrument.Name];
        var estimates = Account.ActivePositions.Select(o => o.Value.GainLossEstimate).ToList();

        point.Instrument = instrument;
        point.TimeFrame = instrument.TimeFrame;

        instrument.Points.Add(point);
        instrument.PointGroups.Add(point, instrument.TimeFrame, true);
      }

      return points;
    }
  }
}
