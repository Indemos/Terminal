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
  public interface IGateway
  {
    /// <summary>
    /// Account
    /// </summary>
    AccountModel Account { get; set; }

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
    Task<DomModel> GetDom(CriteriaModel criteria);

    /// <summary>
    /// Get historical bars
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<PriceModel>> GetBars(CriteriaModel criteria);

    /// <summary>
    /// Get historical ticks
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<PriceModel>> GetTicks(CriteriaModel criteria);

    /// <summary>
    /// Get options
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<InstrumentModel>> GetOptions(CriteriaModel criteria);

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<OrderModel>> GetOrders(CriteriaModel criteria);

    /// <summary>
    /// Get positions
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<OrderModel>> GetPositions(CriteriaModel criteria);

    /// <summary>
    /// Get all account transactions
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<OrderModel>> GetTransactions(CriteriaModel criteria);

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

    /// <summary>
    /// Descriptor
    /// </summary>
    /// <param name="instrument"></param>
    Task<string> Descriptor(string instrument = null);
  }

  public abstract class Gateway : IGateway
  {
    /// <summary>
    /// Grain client
    /// </summary>
    public virtual IClusterClient Connector { get; set; }

    /// <summary>
    /// Messenger
    /// </summary>
    public virtual MessageService Messenger { get; set; }

    /// <summary>
    /// Account
    /// </summary>
    public virtual AccountModel Account { get; set; }

    /// <summary>
    /// Grain namespace
    /// </summary>
    public virtual string Space { get; set; } = Guid.NewGuid().ToString();

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
    public abstract Task<DomModel> GetDom(CriteriaModel criteria);

    /// <summary>
    /// Get historical bars
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<IList<PriceModel>> GetBars(CriteriaModel criteria);

    /// <summary>
    /// Get historical ticks
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<IList<PriceModel>> GetTicks(CriteriaModel criteria);

    /// <summary>
    /// Get options
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<IList<InstrumentModel>> GetOptions(CriteriaModel criteria);

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<IList<OrderModel>> GetOrders(CriteriaModel criteria);

    /// <summary>
    /// Get positions
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<IList<OrderModel>> GetPositions(CriteriaModel criteria);

    /// <summary>
    /// Get all account transactions
    /// </summary>
    /// <param name="criteria"></param>
    public abstract Task<IList<OrderModel>> GetTransactions(CriteriaModel criteria);

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
    public virtual Task<string> Descriptor(string instrument = null) => Task.FromResult(Name(instrument));

    /// <summary>
    /// Descriptor
    /// </summary>
    /// <param name="instrument"></param>
    protected virtual string Name(string instrument = null) => instrument is null ?
      $"{Space}:{Account.Name}" :
      $"{Space}:{Account.Name}:{instrument}";

    /// <summary>
    /// Grain selector
    /// </summary>
    protected virtual T Component<T>(string instrument = null) where T : IGrainWithStringKey => Connector.GetGrain<T>(Name(instrument));

    /// <summary>
    /// Subscribe to account updates
    /// </summary>
    protected virtual void SubscribeToUpdates()
    {
      Messenger.Subscribe<OrderModel>(order =>
      {
        if (order.Operation.Status is OrderStatusEnum.Transaction)
        {
          Account = Account with
          {
            Performance = Account.Performance + order.Balance.Current
          };
        }

        return Task.CompletedTask;

      }, nameof(OrderModel));
    }
  }
}
