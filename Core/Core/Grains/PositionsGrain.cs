using Core.Enums;
using Core.Models;
using Orleans;
using Orleans.Streams;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Grains
{
  public interface IPositionsGrain : IGrainWithStringKey
  {
    /// <summary>
    /// Get positions
    /// </summary>
    /// <param name="criteria"></param>
    Task<OrdersResponse> Positions(Criteria criteria);

    /// <summary>
    /// Store positions
    /// </summary>
    /// <param name="orders"></param>
    Task<StatusResponse> Store(Dictionary<string, Order> orders);

    /// <summary>
    /// Store position
    /// </summary>
    /// <param name="order"></param>
    Task<OrderResponse> Store(Order order);
  }

  public class PositionsGrain : Grain<Dictionary<string, Order>>, IPositionsGrain
  {
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
    /// Get positions
    /// </summary>
    /// <param name="criteria"></param>
    public virtual async Task<OrdersResponse> Positions(Criteria criteria) => new()
    {
      Data = [.. State.Values]
    };

    /// <summary>
    /// Store positions
    /// </summary>
    /// <param name="orders"></param>
    public virtual Task<StatusResponse> Store(Dictionary<string, Order> orders)
    {
      State = orders;

      return Task.FromResult(new StatusResponse()
      {
        Data = StatusEnum.Active
      });
    }

    /// <summary>
    /// Store position
    /// </summary>
    /// <param name="order"></param>
    public virtual Task<OrderResponse> Store(Order order)
    {
      State[order.Operation.Instrument.Name] = order;

      return Task.FromResult(new OrderResponse()
      {
        Data = order
      });
    }
  }
}
