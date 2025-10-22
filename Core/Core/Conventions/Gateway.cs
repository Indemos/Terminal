using Core.Enums;
using Core.Models;
using Core.Services;
using Orleans;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Conventions
{
  public interface IGateway : IDisposable
  {
    /// <summary>
    /// Account
    /// </summary>
    AccountModel Account { get; set; }

    /// <summary>
    /// Data stream
    /// </summary>
    StreamService Streamer { get; set; }

    /// <summary>
    /// Cluster client
    /// </summary>
    IClusterClient Connector { get; set; }

    /// <summary>
    /// Data stream
    /// </summary>
    Func<PriceModel, Task> Subscription { get; set; }

    /// <summary>
    /// Descriptor
    /// </summary>
    string Descriptor(string instrument = null);

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
    Task<StatusResponse> Subscribe(InstrumentModel instrument);

    /// <summary>
    /// Unsubscribe
    /// </summary>
    /// <param name="instrument"></param>
    Task<StatusResponse> Unsubscribe(InstrumentModel instrument);

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
    Task<DomModel> GetDom(MetaModel criteria);

    /// <summary>
    /// Get historical bars
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<PriceModel>> GetBars(MetaModel criteria);

    /// <summary>
    /// Get historical ticks
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<PriceModel>> GetTicks(MetaModel criteria);

    /// <summary>
    /// Get options
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<InstrumentModel>> GetOptions(MetaModel criteria);

    /// <summary>
    /// Get positions
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<OrderModel>> GetPositions(MetaModel criteria);

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<OrderModel>> GetOrders(MetaModel criteria);

    /// <summary>
    /// Get all account transactions
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<OrderModel>> GetTransactions(MetaModel criteria);

    /// <summary>
    /// Send new orders
    /// </summary>
    /// <param name="order"></param>
    Task<OrderGroupsResponse> SendOrder(OrderModel order);

    /// <summary>
    /// Cancel orders
    /// </summary>
    /// <param name="order"></param>
    Task<DescriptorResponse> ClearOrder(OrderModel order);
  }

  /// <summary>
  /// Implementation
  /// </summary>
  public abstract class Gateway : IGateway
  {
    /// <summary>
    /// Namespace
    /// </summary>
    public virtual string Space { get; set; } = $"{Guid.NewGuid()}";

    /// <summary>
    /// Account
    /// </summary>
    public virtual AccountModel Account { get; set; }

    /// <summary>
    /// Cluster client
    /// </summary>
    public virtual IClusterClient Connector { get; set; }

    /// <summary>
    /// Data stream
    /// </summary>
    public virtual Services.StreamService Streamer { get; set; }

    /// <summary>
    /// Data stream
    /// </summary>
    public virtual Func<PriceModel, Task> Subscription { get; set; }

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
    public abstract Task<StatusResponse> Subscribe(InstrumentModel instrument);

    /// <summary>
    /// Unsubscribe
    /// </summary>
    /// <param name="instrument"></param>
    public abstract Task<StatusResponse> Unsubscribe(InstrumentModel instrument);

    /// <summary>
    /// Get latest quote
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<DomModel> GetDom(MetaModel criteria);

    /// <summary>
    /// Get historical bars
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<IList<PriceModel>> GetBars(MetaModel criteria);

    /// <summary>
    /// Get historical ticks
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<IList<PriceModel>> GetTicks(MetaModel criteria);

    /// <summary>
    /// Get options
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<IList<InstrumentModel>> GetOptions(MetaModel criteria);

    /// <summary>
    /// Get positions
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<IList<OrderModel>> GetPositions(MetaModel criteria);

    /// <summary>
    /// Get all account transactions
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<IList<OrderModel>> GetTransactions(MetaModel criteria);

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<IList<OrderModel>> GetOrders(MetaModel criteria);

    /// <summary>
    /// Send new orders
    /// </summary>
    /// <param name="order"></param>
    public abstract Task<OrderGroupsResponse> SendOrder(OrderModel order);

    /// <summary>
    /// Cancel orders
    /// </summary>
    /// <param name="order"></param>
    public abstract Task<DescriptorResponse> ClearOrder(OrderModel order);

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
        Data = StatusEnum.Pause
      };
    }

    /// <summary>
    /// Descriptor
    /// </summary>
    public virtual string Descriptor(string instrument = null) => instrument is null ?
      $"{Space}:{Account.Name}" :
      $"{Space}:{Account.Name}:{instrument}";

    /// <summary>
    /// Subscribe to price updates
    /// </summary>
    protected virtual void ConnectPrices()
    {
      Streamer.Subscribe<PriceModel>(o => Subscription(o));
    }

    /// <summary>
    /// Subscribe to order updates
    /// </summary>
    protected virtual void ConnectOrders()
    {
      Streamer.Subscribe<OrderModel>(o =>
      {
        if (o.Operation.Status is OrderStatusEnum.Transaction)
        {
          Account = Account with
          {
            Performance = Account.Performance + o.Balance.Current
          };
        }
      });
    }

    /// <summary>
    /// Grain selector
    /// </summary>
    protected virtual T Component<T>(string instrument = null) where T : IGrainWithStringKey => Connector.GetGrain<T>(Descriptor(instrument));
  }
}
