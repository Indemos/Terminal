using Core.Conventions;
using Core.Enums;
using Core.Extensions;
using Core.Models;
using Orleans;
using Orleans.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Grains
{
  public interface ITransactionsGrain : IGrainWithStringKey
  {
    /// <summary>
    /// Setup
    /// </summary>
    /// <param name="observer"></param>
    Task<StatusResponse> Setup(ITradeObserver observer);

    /// <summary>
    /// Get transactions
    /// </summary>
    /// <param name="criteria"></param>
    Task<OrdersResponse> Transactions(Criteria criteria);

    /// <summary>
    /// Store transactions
    /// </summary>
    /// <param name="orders"></param>
    Task<StatusResponse> Store(List<Order> orders);

    /// <summary>
    /// Store transaction
    /// </summary>
    /// <param name="order"></param>
    Task<OrderResponse> Store(Order order);
  }

  /// <summary>
  /// Constructor
  /// </summary>
  /// <param name="messenger"></param>
  public class TransactionsGrain : Grain<List<Order>>, ITransactionsGrain
  {
    /// <summary>
    /// Observer
    /// </summary>
    protected ITradeObserver observer;

    /// <summary>
    /// Messenger
    /// </summary>
    protected IAsyncStream<Message> messenger;

    /// <summary>
    /// Activation
    /// </summary>
    /// <param name="cancellation"></param>
    public override async Task OnActivateAsync(CancellationToken cancellation)
    {
      State = [];

      messenger = this
        .GetStreamProvider(nameof(Message))
        .GetStream<Message>(string.Empty, Guid.Empty);

      await base.OnActivateAsync(cancellation);
    }

    /// <summary>
    /// Setup
    /// </summary>
    /// <param name="grainObserver"></param>
    public virtual Task<StatusResponse> Setup(ITradeObserver grainObserver)
    {
      observer = grainObserver;

      return Task.FromResult(new StatusResponse
      {
        Data = StatusEnum.Active
      });
    }

    /// <summary>
    /// Get transactions
    /// </summary>
    /// <param name="criteria"></param>
    public virtual async Task<OrdersResponse> Transactions(Criteria criteria)
    {
      return new OrdersResponse
      {
        Data = State
      };
    }

    /// <summary>
    /// Store transactions
    /// </summary>
    /// <param name="orders"></param>
    public virtual Task<StatusResponse> Store(List<Order> orders)
    {
      State = orders;

      return Task.FromResult(new StatusResponse()
      {
        Data = StatusEnum.Active
      });
    }

    /// <summary>
    /// Store transaction
    /// </summary>
    /// <param name="order"></param>
    public virtual async Task<OrderResponse> Store(Order order)
    {
      State.Add(order);
      observer.StreamOrder(order);

      return new()
      {
        Data = order
      };
    }
  }
}
