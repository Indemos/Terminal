using Distribution.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Core.Enums;
using Terminal.Core.Extensions;
using Terminal.Core.Models;
using Terminal.Core.Services;
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
    /// Point stream
    /// </summary>
    Action<MessageModel<PointModel>> Stream { get; set; }

    /// <summary>
    /// Order stream
    /// </summary>
    Action<MessageModel<OrderModel>> OrderStream { get; set; }

    /// <summary>
    /// Connect
    /// </summary>
    Task<ResponseModel<StatusEnum>> Connect();

    /// <summary>
    /// Disconnect
    /// </summary>
    Task<ResponseModel<StatusEnum>> Disconnect();

    /// <summary>
    /// Subscribe
    /// </summary>
    /// <param name="instrument"></param>
    /// <returns></returns>
    Task<ResponseModel<StatusEnum>> Subscribe(InstrumentModel instrument);

    /// <summary>
    /// Unsubscribe
    /// </summary>
    /// <param name="instrument"></param>
    /// <returns></returns>
    Task<ResponseModel<StatusEnum>> Unsubscribe(InstrumentModel instrument);

    /// <summary>
    /// Subscribe
    /// </summary>
    Task<ResponseModel<StatusEnum>> Subscribe();

    /// <summary>
    /// Unsubscribe
    /// </summary>
    Task<ResponseModel<StatusEnum>> Unsubscribe();

    /// <summary>
    /// Get account state
    /// </summary>
    Task<ResponseModel<IAccount>> GetAccount();

    /// <summary>
    /// Get latest quote
    /// </summary>
    /// <param name="criteria"></param>
    Task<ResponseModel<DomModel>> GetDom(ConditionModel criteria = null);

    /// <summary>
    /// Get historical bars
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    Task<ResponseModel<List<PointModel>>> GetPoints(ConditionModel criteria = null);

    /// <summary>
    /// Get options
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    Task<ResponseModel<List<InstrumentModel>>> GetOptions(ConditionModel criteria = null);

    /// <summary>
    /// Get positions
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    Task<ResponseModel<List<OrderModel>>> GetPositions(ConditionModel criteria = null);

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    Task<ResponseModel<List<OrderModel>>> GetOrders(ConditionModel criteria = null);

    /// <summary>
    /// Send new orders
    /// </summary>
    /// <param name="order"></param>
    Task<ResponseModel<OrderModel>> SendOrder(OrderModel order);

    /// <summary>
    /// Cancel orders
    /// </summary>
    /// <param name="orders"></param>
    Task<ResponseModel<List<OrderModel>>> ClearOrders(params OrderModel[] orders);
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
    /// Point stream
    /// </summary>
    public virtual Action<MessageModel<PointModel>> Stream { get; set; }

    /// <summary>
    /// Order stream
    /// </summary>
    public virtual Action<MessageModel<OrderModel>> OrderStream { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public Gateway()
    {
      Stream = o => { };
      OrderStream = o => { };
    }

    /// <summary>
    /// Connect
    /// </summary>
    public abstract Task<ResponseModel<StatusEnum>> Connect();

    /// <summary>
    /// Disconnect
    /// </summary>
    /// <returns></returns>
    public abstract Task<ResponseModel<StatusEnum>> Disconnect();

    /// <summary>
    /// Subscribe
    /// </summary>
    /// <param name="instrument"></param>
    /// <returns></returns>
    public abstract Task<ResponseModel<StatusEnum>> Subscribe(InstrumentModel instrument);

    /// <summary>
    /// Unsubscribe
    /// </summary>
    /// <param name="instrument"></param>
    /// <returns></returns>
    public abstract Task<ResponseModel<StatusEnum>> Unsubscribe(InstrumentModel instrument);

    /// <summary>
    /// Get account state
    /// </summary>
    public abstract Task<ResponseModel<IAccount>> GetAccount();

    /// <summary>
    /// Get latest quote
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public abstract Task<ResponseModel<DomModel>> GetDom(ConditionModel criteria = null);

    /// <summary>
    /// Get historical bars
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public abstract Task<ResponseModel<List<PointModel>>> GetPoints(ConditionModel criteria = null);

    /// <summary>
    /// Get options
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public abstract Task<ResponseModel<List<InstrumentModel>>> GetOptions(ConditionModel criteria = null);

    /// <summary>
    /// Get positions
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public abstract Task<ResponseModel<List<OrderModel>>> GetPositions(ConditionModel criteria = null);

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public abstract Task<ResponseModel<List<OrderModel>>> GetOrders(ConditionModel criteria = null);

    /// <summary>
    /// Send new orders
    /// </summary>
    /// <param name="order"></param>
    public abstract Task<ResponseModel<OrderModel>> SendOrder(OrderModel order);

    /// <summary>
    /// Cancel orders
    /// </summary>
    /// <param name="orders"></param>
    public abstract Task<ResponseModel<List<OrderModel>>> ClearOrders(params OrderModel[] orders);

    /// <summary>
    /// Dispose
    /// </summary>
    public virtual void Dispose() => Disconnect();

    /// <summary>
    /// Subscribe
    /// </summary>
    public virtual async Task<ResponseModel<StatusEnum>> Subscribe()
    {
      await Task.WhenAll(Account
        .States
        .Values
        .Select(o => Subscribe(o.Instrument)));

      return new ResponseModel<StatusEnum>
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Unsubscribe
    /// </summary>
    public virtual async Task<ResponseModel<StatusEnum>> Unsubscribe()
    {
      await Task.WhenAll(Account
        .States
        .Values
        .Select(o => Unsubscribe(o.Instrument)));

      return new ResponseModel<StatusEnum>
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Create separate orders when combo-orders are not supported
    /// </summary>
    /// <param name="orders"></param>
    public virtual List<OrderModel> ComposeOrders(params OrderModel[] orders)
    {
      OrderModel merge(OrderModel subOrder, OrderModel group)
      {
        var nextOrder = subOrder.Clone() as OrderModel;
        var groupOrders = subOrder
          ?.Orders
          ?.Where(o => o.Instruction is InstructionEnum.Brace)
          ?.Select(o => merge(o, group)) ?? [];

        nextOrder.Descriptor = group.Descriptor;
        nextOrder.Type ??= group.Type ?? OrderTypeEnum.Market;
        nextOrder.TimeSpan ??= group.TimeSpan ?? OrderTimeSpanEnum.Gtc;
        nextOrder.Instruction ??= InstructionEnum.Side;
        nextOrder.Price ??= nextOrder.GetOpenPrice();
        nextOrder.Transaction.Time ??= nextOrder?.Transaction?.Instrument?.Point?.Time;
        nextOrder.Orders = [.. groupOrders];

        return nextOrder;
      }

      return orders.SelectMany(order =>
      {
        var nextOrders = order
          .Orders
          .Where(o => o.Instruction is InstructionEnum.Side or null)
          .Select(o => merge(o, order))
          .ToList();

        if (order.Amount is not null)
        {
          nextOrders.Add(merge(order, order));
        }

        return nextOrders;

      }).ToList();
    }

    /// <summary>
    /// Preprocess order
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    protected virtual async Task<List<ErrorModel>> SubscribeToOrder(OrderModel order)
    {
      var response = new List<ErrorModel>();
      var validator = InstanceService<OrderValidator>.Instance;
      var orders = order.Orders.Append(order);

      foreach (var subOrder in orders)
      {
        var errors = validator
          .Validate(order)
          .Errors
          .Select(error => new ErrorModel { ErrorMessage = error.ErrorMessage });

        response.AddRange(errors);

        if (errors.IsEmpty() && subOrder.Name is not null)
        {
          await Subscribe(subOrder.Transaction.Instrument);
        }
      }

      return response;
    }

    /// <summary>
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="instrument"></param>
    protected virtual InstrumentModel UpdateInstrument(InstrumentModel instrument)
    {
      var summary = Account.States.Get(instrument.Name);
      var position = Account.Positions.Get(instrument.Name);

      summary.Instrument = instrument;

      if (position?.Transaction is not null)
      {
        position.Transaction.Instrument = instrument;
      }

      return instrument;
    }

    /// <summary>
    /// Action wrapper
    /// </summary>
    /// <param name="action"></param>
    protected virtual async Task<ResponseModel<T>> Response<T>(Func<Task<T>> action)
    {
      var response = new ResponseModel<T>();

      try
      {
        response.Data = await action();
      }
      catch (Exception e)
      {
        var message = new MessageModel<string>
        {
          Error = e,
          Content = e.Message,
        };

        response.Errors = [new ErrorModel { ErrorMessage = message.Content }];

        Message(message);
      }

      return response;
    }

    /// <summary>
    /// Action wrapper
    /// </summary>
    /// <param name="action"></param>
    protected virtual void Observe(Action action)
    {
      try
      {
        action();
      }
      catch (Exception e)
      {
        var message = new MessageModel<string>
        {
          Error = e,
          Content = e.Message
        };

        Message(message);
      }
    }

    /// <summary>
    /// Action wrapper
    /// </summary>
    /// <param name="action"></param>
    protected virtual async Task Observe(Func<Task> action)
    {
      try
      {
        await action();
      }
      catch (Exception e)
      {
        var message = new MessageModel<string>
        {
          Error = e,
          Content = e.Message
        };

        Message(message);
      }
    }

    /// <summary>
    /// Action wrapper
    /// </summary>
    /// <param name="message"></param>
    protected virtual void Message(MessageModel<string> message)
    {
      InstanceService<MessageService>.Instance.Update(message);
    }
  }
}
