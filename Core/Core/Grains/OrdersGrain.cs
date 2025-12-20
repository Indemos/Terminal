using Core.Enums;
using Core.Models;
using Core.Validators;
using Orleans;
using Orleans.Streams;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Grains
{
  public interface IOrdersGrain : IGrainWithStringKey
  {
    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    Task<OrdersResponse> Orders(Criteria criteria);

    /// <summary>
    /// Store orders
    /// </summary>
    /// <param name="orders"></param>
    Task<StatusResponse> Store(Dictionary<string, Order> orders);

    /// <summary>
    /// Store order
    /// </summary>
    /// <param name="order"></param>
    Task<OrderResponse> Store(Order order);

    /// <summary>
    /// Remove order from the list
    /// </summary>
    /// <param name="order"></param>
    Task<DescriptorResponse> Clear(Order order);
  }

  public class OrdersGrain : Grain<Dictionary<string, Order>>, IOrdersGrain
  {
    /// <summary>
    /// Order validator
    /// </summary>
    protected OrderValidator orderValidator = new();

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
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    public virtual async Task<OrdersResponse> Orders(Criteria criteria) => new()
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
    /// Store order
    /// </summary>
    /// <param name="order"></param>
    public virtual async Task<OrderResponse> Store(Order order)
    {
      State[order.Id] = order with
      {
        Operation = order.Operation with
        {
          Status = OrderStatusEnum.Order
        }
      };

      return new()
      {
        Data = order
      };
    }

    /// <summary>
    /// Remove order from the list
    /// </summary>
    /// <param name="order"></param>
    public virtual Task<DescriptorResponse> Clear(Order order)
    {
      State.Remove(order.Id);

      return Task.FromResult(new DescriptorResponse
      {
        Data = order.Id
      });
    }
  }
}
