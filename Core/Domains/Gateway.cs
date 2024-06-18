using Distribution.Services;
using System;
using System.Collections;
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
    /// Tape
    /// </summary>
    Action<StateModel<OrderModel>> OrderStream { get; set; }

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
    /// Get latest quote
    /// </summary>
    /// <param name="message"></param>
    /// <param name="props"></param>
    /// <returns></returns>
    Task<ResponseItemModel<IDictionary<string, PointModel>>> GetPoint(PointMessageModel message, Hashtable props = null);

    /// <summary>
    /// Get historical bars
    /// </summary>
    /// <param name="message"></param>
    /// <param name="props"></param>
    /// <returns></returns>
    Task<ResponseItemModel<IList<PointModel>>> GetPoints(PointMessageModel message, Hashtable props = null);

    /// <summary>
    /// Get options
    /// </summary>
    /// <param name="message"></param>
    /// <param name="props"></param>
    /// <returns></returns>
    Task<ResponseItemModel<IList<OptionModel>>> GetOptions(OptionMessageModel message, Hashtable props = null);

    /// <summary>
    /// Send new orders
    /// </summary>
    /// <param name="orders"></param>
    Task<ResponseMapModel<OrderModel>> CreateOrders(params OrderModel[] orders);

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
    public virtual IAccount Account { get; set; }

    /// <summary>
    /// Tape
    /// </summary>
    public virtual Action<StateModel<OrderModel>> OrderStream { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public Gateway()
    {
      OrderStream = o => { };
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
    /// Get latest quote
    /// </summary>
    /// <param name="message"></param>
    /// <param name="props"></param>
    /// <returns></returns>
    public abstract Task<ResponseItemModel<IDictionary<string, PointModel>>> GetPoint(PointMessageModel message, Hashtable props = null);

    /// <summary>
    /// Get historical bars
    /// </summary>
    /// <param name="message"></param>
    /// <param name="props"></param>
    /// <returns></returns>
    public abstract Task<ResponseItemModel<IList<PointModel>>> GetPoints(PointMessageModel message, Hashtable props = null);

    /// <summary>
    /// Get options
    /// </summary>
    /// <param name="message"></param>
    /// <param name="props"></param>
    /// <returns></returns>
    public abstract Task<ResponseItemModel<IList<OptionModel>>> GetOptions(OptionMessageModel message, Hashtable props = null);

    /// <summary>
    /// Send new orders
    /// </summary>
    /// <param name="orders"></param>
    public abstract Task<ResponseMapModel<OrderModel>> CreateOrders(params OrderModel[] orders);

    /// <summary>
    /// Cancel orders
    /// </summary>
    /// <param name="orders"></param>
    public abstract Task<ResponseMapModel<OrderModel>> DeleteOrders(params OrderModel[] orders);

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
      var orderRules = InstanceService<OrderPriceValidator>.Instance;
      var response = new ResponseMapModel<OrderModel>();

      foreach (var order in orders)
      {
        var errors = new List<ErrorModel>();

        errors.AddRange(orderRules.Validate(order).Errors.Select(o => new ErrorModel
        {
          ErrorCode = o.ErrorCode,
          ErrorMessage = o.ErrorMessage,
          PropertyName = o.PropertyName
        }));

        errors.AddRange(order.Orders.SelectMany(o => orderRules.Validate(o).Errors.Select(o => new ErrorModel
        {
          ErrorCode = o.ErrorCode,
          ErrorMessage = o.ErrorMessage,
          PropertyName = o.PropertyName
        })));

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
