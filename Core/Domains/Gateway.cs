using Distribution.Services;
using FluentValidation.Results;
using Mapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Core.Enums;
using Terminal.Core.Models;
using Terminal.Core.Validators;

namespace Terminal.Core.Domains
{
  public interface IGateway : IDisposable
  {
    /// <summary>
    /// Account
    /// </summary>
    IAccount Account { get; set; }

    /// <summary>
    /// Connect
    /// </summary>
    Task<IList<ErrorModel>> Connect();

    /// <summary>
    /// Disconnect
    /// </summary>
    Task<IList<ErrorModel>> Disconnect();

    /// <summary>
    /// Subscribe
    /// </summary>
    Task<IList<ErrorModel>> Subscribe(string name);

    /// <summary>
    /// Unsubscribe
    /// </summary>
    Task<IList<ErrorModel>> Unsubscribe(string name);

    /// <summary>
    /// Get quote
    /// </summary>
    /// <param name="message"></param>
    Task<ResponseItemModel<PointModel?>> GetPoint(PointMessageModel message);

    /// <summary>
    /// Get quotes history
    /// </summary>
    /// <param name="message"></param>
    Task<ResponseItemModel<IList<PointModel?>>> GetPoints(PointMessageModel message);

    /// <summary>
    /// Get option chains
    /// </summary>
    /// <param name="message"></param>
    Task<ResponseItemModel<IList<OptionModel>>> GetOptions(OptionMessageModel message);

    /// <summary>
    /// Send new orders
    /// </summary>
    /// <param name="orders"></param>
    Task<ResponseMapModel<OrderModel>> CreateOrders(params OrderModel[] orders);

    /// <summary>
    /// Update orders
    /// </summary>
    /// <param name="orders"></param>
    Task<ResponseMapModel<OrderModel>> UpdateOrders(params OrderModel[] orders);

    /// <summary>
    /// Cancel orders
    /// </summary>
    /// <param name="orders"></param>
    Task<ResponseMapModel<OrderModel>> DeleteOrders(params OrderModel[] orders);
  }

  /// <summary>
  /// Implementation
  /// </summary>
  public abstract class Gateway : IGateway
  {
    /// <summary>
    /// Account
    /// </summary>
    public IAccount Account { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public Gateway()
    {
    }

    /// <summary>
    /// Connect
    /// </summary>
    public abstract Task<IList<ErrorModel>> Connect();

    /// <summary>
    /// Subscribe
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public abstract Task<IList<ErrorModel>> Subscribe(string name);

    /// <summary>
    /// Disconnect
    /// </summary>
    /// <returns></returns>
    public abstract Task<IList<ErrorModel>> Disconnect();

    /// <summary>
    /// Unsubscribe
    /// </summary>
    public abstract Task<IList<ErrorModel>> Unsubscribe(string name);

    /// <summary>
    /// Get quote
    /// </summary>
    /// <param name="message"></param>
    public abstract Task<ResponseItemModel<PointModel?>> GetPoint(PointMessageModel message);

    /// <summary>
    /// Get quotes history
    /// </summary>
    /// <param name="message"></param>
    public abstract Task<ResponseItemModel<IList<PointModel?>>> GetPoints(PointMessageModel message);

    /// <summary>
    /// Get option chains
    /// </summary>
    /// <param name="message"></param>
    public abstract Task<ResponseItemModel<IList<OptionModel>>> GetOptions(OptionMessageModel message);

    /// <summary>
    /// Send new orders
    /// </summary>
    /// <param name="orders"></param>
    public abstract Task<ResponseMapModel<OrderModel>> CreateOrders(params OrderModel[] orders);

    /// <summary>
    /// Update orders
    /// </summary>
    /// <param name="orders"></param>
    public abstract Task<ResponseMapModel<OrderModel>> UpdateOrders(params OrderModel[] orders);

    /// <summary>
    /// Cancel orders
    /// </summary>
    /// <param name="orders"></param>
    public abstract Task<ResponseMapModel<OrderModel>> DeleteOrders(params OrderModel[] orders);

    /// <summary>
    /// Dispose
    /// </summary>
    public void Dispose() => Disconnect();

    /// <summary>
    /// Set missing order properties
    /// </summary>
    /// <param name="orders"></param>
    protected IList<OrderModel> CorrectOrders(params OrderModel[] orders)
    {
      return orders.Select(nextOrder =>
      {
        nextOrder.Type ??= OrderTypeEnum.Market;
        nextOrder.TimeSpan ??= OrderTimeSpanEnum.GTC;

        var action = nextOrder.Transaction ?? new TransactionModel();

        action.Time ??= DateTime.Now;
        action.Price ??= GetOpenPrice(nextOrder);
        action.Status ??= OrderStatusEnum.None;
        action.Operation ??= OperationEnum.In;

        nextOrder.Transaction = action;

        return nextOrder;

      }).ToList();
    }

    /// <summary>
    /// Ensure all properties have correct values
    /// </summary>
    /// <param name="orders"></param>
    protected ResponseMapModel<OrderModel> ValidateOrders(params OrderModel[] orders)
    {
      var map = Mapper<ValidationFailure, ErrorModel>.Map;
      var orderRules = InstanceService<OrderPriceValidator>.Instance;
      var response = new ResponseMapModel<OrderModel>();

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
    protected double? GetOpenPrice(OrderModel nextOrder)
    {
      var pointModel = nextOrder.Transaction?.Instrument?.Points?.LastOrDefault();

      if (pointModel is not null)
      {
        switch (nextOrder.Side)
        {
          case OrderSideEnum.Buy: return pointModel?.Ask;
          case OrderSideEnum.Sell: return pointModel?.Bid;
        }
      }

      return null;
    }

    /// <summary>
    /// Update points
    /// </summary>
    /// <param name="point"></param>
    protected IList<IAccount> SetupAccounts(params IAccount[] accounts)
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
    protected IList<PointModel?> SetupPoints(params PointModel?[] points)
    {
      foreach (var o in points)
      {
        var point = o ?? new PointModel();
        var instrument = Account.Instruments[point.Instrument.Name];
        var estimates = Account.ActivePositions.Select(o => o.Value?.GainLossEstimate).ToList();

        point.Instrument = instrument;
        point.TimeFrame = instrument.TimeFrame;

        instrument.Points.Add(point);
        instrument.PointGroups.Add(point, instrument.TimeFrame, true);
      }

      return points;
    }
  }
}
