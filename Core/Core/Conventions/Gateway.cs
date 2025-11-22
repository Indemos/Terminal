using Core.Enums;
using Core.Models;
using Orleans;
using Orleans.Streams;
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
    void StreamPrice(Instrument instrument);

    /// <summary>
    /// Price message
    /// </summary>
    /// <param name="instrument"></param>
    Task StreamTrade(Instrument instrument);
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
    /// Price message
    /// </summary>
    Action<Instrument> OnPrice { get; set; }

    /// <summary>
    /// Trade message
    /// </summary>
    Func<Instrument, Task> OnTrade { get; set; }

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
    Task<DomResponse> GetDom(Criteria criteria);

    /// <summary>
    /// Get historical ticks
    /// </summary>
    /// <param name="criteria"></param>
    Task<PricesResponse> GetPrices(Criteria criteria);

    /// <summary>
    /// Get historical bars
    /// </summary>
    /// <param name="criteria"></param>
    Task<PricesResponse> GetPriceGroups(Criteria criteria);

    /// <summary>
    /// Get options
    /// </summary>
    /// <param name="criteria"></param>
    Task<InstrumentsResponse> GetOptions(Criteria criteria);

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    Task<OrdersResponse> GetOrders(Criteria criteria);

    /// <summary>
    /// Get positions
    /// </summary>
    /// <param name="criteria"></param>
    Task<OrdersResponse> GetPositions(Criteria criteria);

    /// <summary>
    /// Get all account transactions
    /// </summary>
    /// <param name="criteria"></param>
    Task<OrdersResponse> GetTransactions(Criteria criteria);

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
    /// Price message
    /// </summary>
    public virtual Action<Instrument> OnPrice { get; set; } = o => { };

    /// <summary>
    /// Trade message
    /// </summary>
    public virtual Func<Instrument, Task> OnTrade { get; set; } = o => Task.CompletedTask;

    /// <summary>
    /// Order message
    /// </summary>
    /// <param name="order"></param>
    public virtual void StreamOrder(Order order) => OnOrder(order);

    /// <summary>
    /// Price message
    /// </summary>
    /// <param name="instrument"></param>
    public virtual void StreamPrice(Instrument instrument) => OnPrice(instrument);

    /// <summary>
    /// Price message
    /// </summary>
    /// <param name="instrument"></param>
    public virtual Task StreamTrade(Instrument instrument) => OnTrade(instrument);

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
    /// Get latest quote
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<DomResponse> GetDom(Criteria criteria);

    /// <summary>
    /// Get historical ticks
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<PricesResponse> GetPrices(Criteria criteria);

    /// <summary>
    /// Get historical bars
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<PricesResponse> GetPriceGroups(Criteria criteria);

    /// <summary>
    /// Get options
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<InstrumentsResponse> GetOptions(Criteria criteria);

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<OrdersResponse> GetOrders(Criteria criteria);

    /// <summary>
    /// Get positions
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<OrdersResponse> GetPositions(Criteria criteria);

    /// <summary>
    /// Get all account transactions
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<OrdersResponse> GetTransactions(Criteria criteria);

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
        Data = StatusEnum.Pause
      };
    }

    /// <summary>
    /// Descriptor
    /// </summary>
    /// <param name="instrument"></param>
    protected virtual string Descriptor(string instrument = null) => instrument is null ?
      $"{Space}:{Account.Descriptor}" :
      $"{Space}:{Account.Descriptor}:{instrument}";

    /// <summary>
    /// Grain selector
    /// </summary>
    protected virtual T Component<T>(string instrument = null) where T : IGrainWithStringKey
    {
      return Connector.GetGrain<T>(Descriptor(instrument));
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
            Performance = Account.Performance + position.Balance.Current
          };
        }
      };
    }
  }
}
