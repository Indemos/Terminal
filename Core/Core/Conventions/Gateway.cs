using Core.Enums;
using Core.Grains;
using Core.Models;
using Orleans;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Conventions
{
  /// <summary>
  /// Grain messenger
  /// </summary>
  public interface ITradeObserver : IGrainObserver
  {
    /// <summary>
    /// Order message
    /// </summary>
    /// <param name="order"></param>
    void StreamOrder(Order order);

    /// <summary>
    /// Price message
    /// </summary>
    /// <param name="instrument"></param>
    Task StreamInstrument(Instrument instrument);
  }

  public interface IGateway
  {
    /// <summary>
    /// Account
    /// </summary>
    Account Account { get; set; }

    /// <summary>
    /// Order message
    /// </summary>
    Action<Order> OnOrder { get; set; }

    /// <summary>
    /// Trade message
    /// </summary>
    Func<Instrument, Task> OnInstrument { get; set; }

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
    Task<StatusResponse> Subscribe(Instrument instrument);

    /// <summary>
    /// Unsubscribe
    /// </summary>
    /// <param name="instrument"></param>
    Task<StatusResponse> Unsubscribe(Instrument instrument);

    /// <summary>
    /// Subscribe
    /// </summary>
    Task<StatusResponse> Subscribe();

    /// <summary>
    /// Unsubscribe
    /// </summary>
    Task<StatusResponse> Unsubscribe();

    /// <summary>
    /// Get latest quote
    /// </summary>
    /// <param name="criteria"></param>
    Task<DomResponse> GetDom(DomCriteria criteria);

    /// <summary>
    /// Get historical ticks
    /// </summary>
    /// <param name="criteria"></param>
    Task<PricesResponse> GetPrices(PriceCriteria criteria);

    /// <summary>
    /// Get historical bars
    /// </summary>
    /// <param name="criteria"></param>
    Task<PricesResponse> GetPriceGroups(PriceCriteria criteria);

    /// <summary>
    /// Get options
    /// </summary>
    /// <param name="criteria"></param>
    Task<InstrumentsResponse> GetOptions(OptionCriteria criteria);

    /// <summary>
    /// Get order
    /// </summary>
    /// <param name="criteria"></param>
    Task<OrderResponse> GetOrder(OrderCriteria criteria);

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    Task<OrdersResponse> GetOrders(OrderCriteria criteria);

    /// <summary>
    /// Get position
    /// </summary>
    /// <param name="criteria"></param>
    Task<OrderResponse> GetPosition(PositionCriteria criteria);

    /// <summary>
    /// Get positions
    /// </summary>
    /// <param name="criteria"></param>
    Task<OrdersResponse> GetPositions(PositionCriteria criteria);

    /// <summary>
    /// Get transactions
    /// </summary>
    /// <param name="criteria"></param>
    Task<OrdersResponse> GetTransactions(TransactionCriteria criteria);

    /// <summary>
    /// Send new orders
    /// </summary>
    /// <param name="order"></param>
    Task<OrderResponse> SendOrder(Order order);

    /// <summary>
    /// Cancel orders
    /// </summary>
    /// <param name="order"></param>
    Task<DescriptorResponse> ClearOrder(Order order);
  }

  public abstract class Gateway : IGateway, ITradeObserver
  {
    /// <summary>
    /// Grain client
    /// </summary>
    public virtual IClusterClient Connector { get; set; }

    /// <summary>
    /// Account
    /// </summary>
    public virtual Account Account { get; set; }

    /// <summary>
    /// Grain namespace
    /// </summary>
    public virtual string Space { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Order message
    /// </summary>
    public virtual Action<Order> OnOrder { get; set; } = o => { };

    /// <summary>
    /// Trade message
    /// </summary>
    public virtual Func<Instrument, Task> OnInstrument { get; set; } = o => Task.CompletedTask;

    /// <summary>
    /// Order message
    /// </summary>
    /// <param name="order"></param>
    public virtual void StreamOrder(Order order) => OnOrder(order); 

    /// <summary>
    /// Price message
    /// </summary>
    /// <param name="instrument"></param>
    public virtual Task StreamInstrument(Instrument instrument) => OnInstrument(instrument);

    /// <summary>
    /// Connect
    /// </summary>
    public abstract Task<StatusResponse> Connect();

    /// <summary>
    /// Disconnect
    /// </summary>
    public abstract Task<StatusResponse> Disconnect();

    /// <summary>
    /// Subscribe
    /// </summary>
    /// <param name="instrument"></param>
    public abstract Task<StatusResponse> Subscribe(Instrument instrument);

    /// <summary>
    /// Unsubscribe
    /// </summary>
    /// <param name="instrument"></param>
    public abstract Task<StatusResponse> Unsubscribe(Instrument instrument);

    /// <summary>
    /// Send new orders
    /// </summary>
    /// <param name="order"></param>
    public abstract Task<OrderResponse> SendOrder(Order order);

    /// <summary>
    /// Cancel orders
    /// </summary>
    /// <param name="order"></param>
    public abstract Task<DescriptorResponse> ClearOrder(Order order);

    /// <summary>
    /// Get latest quote
    /// </summary>
    /// <param name="criteria"></param>
    public virtual Task<DomResponse> GetDom(DomCriteria criteria)
    {
      return Component<IDomGrain>(criteria.Instrument.Name).Dom(criteria);
    }

    /// <summary>
    /// Get historical ticks
    /// </summary>
    /// <param name="criteria"></param>
    public virtual Task<PricesResponse> GetPrices(PriceCriteria criteria)
    {
      return Component<IInstrumentGrain>(criteria.Instrument.Name).Prices(criteria);
    }

    /// <summary>
    /// Get historical bars
    /// </summary>
    /// <param name="criteria"></param>
    public virtual Task<PricesResponse> GetPriceGroups(PriceCriteria criteria)
    {
      return Component<IInstrumentGrain>(criteria.Instrument.Name).PriceGroups(criteria);
    }

    /// <summary>
    /// Get options
    /// </summary>
    /// <param name="criteria"></param>
    public virtual Task<InstrumentsResponse> GetOptions(OptionCriteria criteria)
    {
      return Component<IOptionsGrain>(criteria.Instrument.Name).Options(criteria);
    }

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    public virtual Task<OrdersResponse> GetOrders(OrderCriteria criteria)
    {
      return Component<IOrdersGrain>().Orders(criteria);
    }

    /// <summary>
    /// Get positions
    /// </summary>
    /// <param name="criteria"></param>
    public virtual Task<OrdersResponse> GetPositions(PositionCriteria criteria)
    {
      return Component<IPositionsGrain>().Positions(criteria);
    }

    /// <summary>
    /// Get all account transactions
    /// </summary>
    /// <param name="criteria"></param>
    public virtual Task<OrdersResponse> GetTransactions(TransactionCriteria criteria)
    {
      return Component<ITransactionsGrain>().Transactions(criteria);
    }

    /// <summary>
    /// Get order
    /// </summary>
    /// <param name="criteria"></param>
    public virtual async Task<OrderResponse> GetOrder(OrderCriteria criteria)
    {
      var orders = await GetOrders(criteria);

      return new()
      {
        Errors = orders.Errors,
        Data = orders.Data.FirstOrDefault()
      };
    }

    /// <summary>
    /// Get position
    /// </summary>
    /// <param name="criteria"></param>
    public virtual async Task<OrderResponse> GetPosition(PositionCriteria criteria)
    {
      var positions = await GetPositions(criteria);

      return new()
      {
        Errors = positions.Errors,
        Data = positions.Data.FirstOrDefault()
      };
    }

    /// <summary>
    /// Subscribe
    /// </summary>
    public virtual async Task<StatusResponse> Subscribe()
    {
      await Task.WhenAll(Account
        .Instruments
        .Values
        .Select(Subscribe));

      return new()
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

      return new()
      {
        Data = StatusEnum.Pause
      };
    }

    /// <summary>
    /// Descriptor
    /// </summary>
    /// <param name="name"></param>
    protected virtual string Descriptor(string name = null) => name is null ?
      $"{Space}:{Account.Descriptor}" :
      $"{Space}:{Account.Descriptor}:{name}";

    /// <summary>
    /// Criteria selector
    /// <param name="name"></param>
    /// </summary>
    protected virtual T Criteria<T>(Criteria criteria) where T : IGrainWithStringKey => criteria is null ?
      Component<T>($"{typeof(Criteria).Name}") :
      Component<T>($"{typeof(Criteria).Name}:{criteria with { Source = false }}");

    /// <summary>
    /// Grain selector
    /// <param name="name"></param>
    /// </summary>
    protected virtual T Component<T>(string name = null) where T : IGrainWithStringKey
    {
      return Connector.GetGrain<T>(Descriptor(name));
    }

    /// <summary>
    /// Subscribe to account updates
    /// </summary>
    protected virtual void SubscribeToUpdates()
    {
      OnOrder += position =>
      {
        if (position.Operation.Status is OrderStatusEnum.Transaction)
        {
          Account = Account with
          {
            Performance = (Account.Performance ?? 0) + (position.Balance.Current ?? 0)
          };
        }
      };
    }
  }
}
