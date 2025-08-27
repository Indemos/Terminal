using Core.Common.Enums;
using Core.Common.Extensions;
using Core.Common.Services;
using Core.Common.States;
using Core.Common.Validators;
using Orleans;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Common.Grains
{
  public interface IGateway
  {
    /// <summary>
    /// Account
    /// </summary>
    AccountState Account { get; set; }

    /// <summary>
    /// Cluster client
    /// </summary>
    IClusterClient GrainFactory { get; set; }

    /// <summary>
    /// Connect
    /// </summary>
    Task<StatusResponse> Connect();

    /// <summary>
    /// Disconnect
    /// </summary>
    Task<StatusResponse> Disconnect();

    /// <summary>
    /// Subscribe
    /// </summary>
    /// <param name="instrument"></param>
    Task<StatusResponse> Subscribe(InstrumentState instrument);

    /// <summary>
    /// Unsubscribe
    /// </summary>
    /// <param name="instrument"></param>
    Task<StatusResponse> Unsubscribe(InstrumentState instrument);

    /// <summary>
    /// Subscribe
    /// </summary>
    Task<StatusResponse> Subscribe();

    /// <summary>
    /// Unsubscribe
    /// </summary>
    Task<StatusResponse> Unsubscribe();

    /// <summary>
    /// Get account state
    /// </summary>
    Task<AccountResponse> GetAccount();

    /// <summary>
    /// Get latest quote
    /// </summary>
    /// <param name="criteria"></param>
    Task<DomResponse> GetDom(ConditionState criteria = null);

    /// <summary>
    /// Get historical bars
    /// </summary>
    /// <param name="criteria"></param>
    Task<PricesResponse> GetBars(ConditionState criteria = null);

    /// <summary>
    /// Get historical ticks
    /// </summary>
    /// <param name="criteria"></param>
    Task<PricesResponse> GetTicks(ConditionState criteria = null);

    /// <summary>
    /// Get options
    /// </summary>
    /// <param name="criteria"></param>
    Task<InstrumentsResponse> GetOptions(ConditionState criteria = null);

    /// <summary>
    /// Get positions
    /// </summary>
    /// <param name="criteria"></param>
    Task<OrdersResponse> GetPositions(ConditionState criteria = null);

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    Task<OrdersResponse> GetOrders(ConditionState criteria = null);

    /// <summary>
    /// Get all account transactions
    /// </summary>
    /// <param name="criteria"></param>
    Task<OrdersResponse> GetTransactions(ConditionState criteria = null);

    /// <summary>
    /// Send new orders
    /// </summary>
    /// <param name="order"></param>
    Task<OrderGroupsResponse> SendOrder(OrderState order);

    /// <summary>
    /// Cancel orders
    /// </summary>
    /// <param name="order"></param>
    Task<DescriptorResponse> ClearOrder(OrderState order);
  }

  /// <summary>
  /// Implementation
  /// </summary>
  public abstract class Gateway : IGateway
  {
    /// <summary>
    /// Account
    /// </summary>
    public virtual AccountState Account { get; set; }

    /// <summary>
    /// Cluster client
    /// </summary>
    public virtual IClusterClient GrainFactory { get; set; }

    /// <summary>
    /// Connect
    /// </summary>
    public abstract Task<StatusResponse> Connect();

    /// <summary>
    /// Disconnect
    /// </summary>
    /// <returns></returns>
    public abstract Task<StatusResponse> Disconnect();

    /// <summary>
    /// Subscribe
    /// </summary>
    /// <param name="instrument"></param>
    /// <returns></returns>
    public abstract Task<StatusResponse> Subscribe(InstrumentState instrument);

    /// <summary>
    /// Unsubscribe
    /// </summary>
    /// <param name="instrument"></param>
    /// <returns></returns>
    public abstract Task<StatusResponse> Unsubscribe(InstrumentState instrument);

    /// <summary>
    /// Get account state
    /// </summary>
    public abstract Task<AccountResponse> GetAccount();

    /// <summary>
    /// Get latest quote
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public abstract Task<DomResponse> GetDom(ConditionState criteria = null);

    /// <summary>
    /// Get historical bars
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public abstract Task<PricesResponse> GetBars(ConditionState criteria = null);

    /// <summary>
    /// Get historical ticks
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public abstract Task<PricesResponse> GetTicks(ConditionState criteria = null);

    /// <summary>
    /// Get options
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public abstract Task<InstrumentsResponse> GetOptions(ConditionState criteria = null);

    /// <summary>
    /// Get positions
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public abstract Task<OrdersResponse> GetPositions(ConditionState criteria = null);

    /// <summary>
    /// Get all account transactions
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<OrdersResponse> GetTransactions(ConditionState criteria = null);

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public abstract Task<OrdersResponse> GetOrders(ConditionState criteria = null);

    /// <summary>
    /// Send new orders
    /// </summary>
    /// <param name="order"></param>
    public abstract Task<OrderGroupsResponse> SendOrder(OrderState order);

    /// <summary>
    /// Cancel orders
    /// </summary>
    /// <param name="order"></param>
    public abstract Task<DescriptorResponse> ClearOrder(OrderState order);

    /// <summary>
    /// Dispose
    /// </summary>
    public virtual void Dispose() => Disconnect();

    /// <summary>
    /// Subscribe
    /// </summary>
    public virtual async Task<StatusResponse> Subscribe()
    {
      await Task.WhenAll(Account
        .Instruments
        .Values
        .Select(Subscribe));

      return new StatusResponse
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Unsubscribe
    /// </summary>
    public virtual async Task<StatusResponse> Unsubscribe()
    {
      await Task.WhenAll(Account
        .Instruments
        .Values
        .Select(Unsubscribe));

      return new StatusResponse
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Convert hierarchy of orders into a plain list
    /// </summary>
    protected virtual List<OrderState> Compose(OrderState order)
    {
      var nextOrders = order
        .Orders
        .Where(o => o.Instruction is InstructionEnum.Side or null)
        .Select(o => Merge(o, order))
        .ToList();

      if (order.Amount is not null)
      {
        nextOrders.Add(Merge(order, order));
      }

      return nextOrders;
    }

    /// <summary>
    /// Update side order from group
    /// </summary>
    /// <param name="group"></param>
    /// <param name="order"></param>
    protected virtual OrderState Merge(OrderState group, OrderState order)
    {
      var point = Account
        .Instruments
        .Get(order.Operation.Instrument.Name)
        .Price;

      var openPrice = order.Side switch
      {
        OrderSideEnum.Long => point.Ask,
        OrderSideEnum.Short => point.Bid,
        _ => null
      };

      var groupOrders = order
        ?.Orders
        ?.Where(o => o.Instruction is InstructionEnum.Brace)
        ?.Select(o => Merge(group, o));

      var nextOrder = order with
      {
        Descriptor = group.Descriptor,
        Type = order.Type ?? group.Type ?? OrderTypeEnum.Market,
        TimeSpan = order.TimeSpan ?? group.TimeSpan ?? OrderTimeSpanEnum.Gtc,
        Instruction = order.Instruction ?? InstructionEnum.Side,
        Price = order.Price ?? openPrice,
        Operation = order.Operation with { Time = order?.Operation?.Instrument?.Price?.Time },
        Orders = [.. groupOrders]
      };

      return nextOrder;
    }

    /// <summary>
    /// Preprocess order
    /// </summary>
    /// <param name="order"></param>
    protected virtual List<ErrorState> GetErrors(OrderState order)
    {
      var response = new List<ErrorState>();
      var validator = InstanceService<OrderValidator>.Instance;
      var orders = order.Orders.Append(order);

      foreach (var subOrder in orders)
      {
        var errors = validator
          .Validate(subOrder)
          .Errors
          .Select(error => new ErrorState { Message = error.ErrorMessage });

        response.AddRange(errors);
      }

      return response;
    }
  }
}
