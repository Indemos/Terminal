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
    Task<ResponseMapModel<OrderModel>> SendOrders(params OrderModel[] orders);

    /// <summary>
    /// Cancel orders
    /// </summary>
    /// <param name="orders"></param>
    Task<ResponseMapModel<OrderModel>> CancelOrders(params OrderModel[] orders);
  }

  /// <summary>
  /// Implementation
  /// </summary>
  public abstract class Gateway : IGateway
  {
    /// <summary>
    /// Account
    /// </summary>
    public virtual IAccount Account { get; set; }

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
    public abstract Task<ResponseMapModel<OrderModel>> SendOrders(params OrderModel[] orders);

    /// <summary>
    /// Cancel orders
    /// </summary>
    /// <param name="orders"></param>
    public abstract Task<ResponseMapModel<OrderModel>> CancelOrders(params OrderModel[] orders);

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
        nextOrder.TimeSpan ??= OrderTimeSpanEnum.Gtc;
        nextOrder.Price ??= GetOpenPrice(nextOrder);
        nextOrder.Transaction ??= new TransactionModel();
        nextOrder.Transaction.Time ??= DateTime.Now;
        nextOrder.Transaction.Status ??= OrderStatusEnum.None;
        nextOrder.Transaction.Operation ??= OperationEnum.In;
      }

      return orders;
    }

    /// <summary>
    /// Ensure all properties have correct values
    /// </summary>
    /// <param name="orders"></param>
    protected virtual ResponseMapModel<OrderModel> ValidateOrders(params OrderModel[] orders)
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
    protected virtual decimal? GetOpenPrice(OrderModel nextOrder)
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
    protected virtual IList<IAccount> SetupAccounts(params IAccount[] accounts)
    {
      foreach (var account in accounts)
      {
        account.InitialBalance = account.Balance;
      }

      return accounts;
    }
  }
}
