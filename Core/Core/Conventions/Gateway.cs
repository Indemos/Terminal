using Core.Enums;
using Core.Extensions;
using Core.Models;
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
    /// Namespace
    /// </summary>
    string Space { get; set; }

    /// <summary>
    /// Account
    /// </summary>
    AccountModel Account { get; set; }

    /// <summary>
    /// Data stream
    /// </summary>
    Services.StreamService Streamer { get; set; }

    /// <summary>
    /// Cluster client
    /// </summary>
    IClusterClient Connector { get; set; }

    /// <summary>
    /// Data stream
    /// </summary>
    Func<PriceModel, Task> Subscription { get; set; }

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
    Task<DomModel> Dom(MetaModel criteria);

    /// <summary>
    /// Get historical bars
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<PriceModel>> Bars(MetaModel criteria);

    /// <summary>
    /// Get historical ticks
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<PriceModel>> Ticks(MetaModel criteria);

    /// <summary>
    /// Get options
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<InstrumentModel>> Options(MetaModel criteria);

    /// <summary>
    /// Get positions
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<OrderModel>> Positions(MetaModel criteria);

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<OrderModel>> Orders(MetaModel criteria);

    /// <summary>
    /// Get all account transactions
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<OrderModel>> Transactions(MetaModel criteria);

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
    public abstract Task<DomModel> Dom(MetaModel criteria);

    /// <summary>
    /// Get historical bars
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<IList<PriceModel>> Bars(MetaModel criteria);

    /// <summary>
    /// Get historical ticks
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<IList<PriceModel>> Ticks(MetaModel criteria);

    /// <summary>
    /// Get options
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<IList<InstrumentModel>> Options(MetaModel criteria);

    /// <summary>
    /// Get positions
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<IList<OrderModel>> Positions(MetaModel criteria);

    /// <summary>
    /// Get all account transactions
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<IList<OrderModel>> Transactions(MetaModel criteria);

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<IList<OrderModel>> Orders(MetaModel criteria);

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
    /// Generate descriptor
    /// </summary>
    /// <param name="instrument"></param>
    protected virtual DescriptorModel Descriptor(string instrument = null) => new()
    {
      Space = Space,
      Account = Account.Name,
      Instrument = instrument
    };

    /// <summary>
    /// Descriptor
    /// </summary>
    protected virtual T Component<T>(string instrument = null) where T : IGrainWithStringKey => Connector.Get<T>(Descriptor(instrument));
  }
}
