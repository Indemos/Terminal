using Core.Enums;
using Core.Extensions;
using Core.Models;
using Core.Validators;
using Orleans;
using System.Collections.Generic;
using System.Linq;
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
    /// Send order
    /// </summary>
    /// <param name="order"></param>
    Task<OrderGroupResponse> Send(Order order);

    /// <summary>
    /// Update order data
    /// </summary>
    /// <param name="instrument"></param>
    Task<StatusResponse> Tap(Instrument instrument);

    /// <summary>
    /// Remove order from the list
    /// </summary>
    /// <param name="order"></param>
    Task<DescriptorResponse> Clear(Order order);

    /// <summary>
    /// Clear orders
    /// </summary>
    Task<StatusResponse> Clear();
  }

  public class OrdersGrain : Grain<Orders>, IOrdersGrain
  {
    /// <summary>
    /// Order validator
    /// </summary>
    protected OrderValidator orderValidator = new();

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    public virtual async Task<OrdersResponse> Orders(Criteria criteria)
    {
      var items = await Task.WhenAll(State
        .Grains
        .Values
        .Select(o => o.Order()));

      return new()
      {
        Data = items
      };
    }

    /// <summary>
    /// Send order
    /// </summary>
    /// <param name="order"></param>
    public virtual async Task<OrderResponse> Store(Order order)
    {
      var descriptor = this.GetDescriptor(order.Id);
      var grain = GrainFactory.GetGrain<IOrderGrain>(descriptor);

      await grain.Store(order with
      {
        Operation = order.Operation with
        {
          Status = OrderStatusEnum.Order
        }
      });

      State.Grains[order.Id] = grain;

      return new()
      {
        Data = order
      };
    }

    /// <summary>
    /// Send order
    /// </summary>
    /// <param name="order"></param>
    public virtual async Task<OrderGroupResponse> Send(Order order)
    {
      var orders = Compose(order);
      var responses = orders
        .Select(o => new OrderResponse { Data = o, Errors = [.. Errors(o).Select(error => error.Message)] })
        .ToArray();

      if (responses.Sum(o => o.Errors.Count) is 0)
      {
        foreach (var subOrder in orders)
        {
          await Store(subOrder);
        }
      }

      return new()
      {
        Data = responses
      };
    }

    /// <summary>
    /// Update order data
    /// </summary>
    /// <param name="instrument"></param>
    public virtual async Task<StatusResponse> Tap(Instrument instrument)
    {
      var descriptor = this.GetDescriptor();
      var positionsGrain = GrainFactory.GetGrain<IPositionsGrain>(descriptor);

      foreach (var grain in State.Grains)
      {
        var response = await grain.Value.Tap(instrument);

        if (response.Data is not null)
        {
          State.Grains.Remove(grain.Key);
          await positionsGrain.Store(response.Data);
        }
      }

      return new()
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Remove order from the list
    /// </summary>
    /// <param name="order"></param>
    public virtual Task<DescriptorResponse> Clear(Order order)
    {
      State.Grains.Remove(order.Id);

      return Task.FromResult(new DescriptorResponse
      {
        Data = order.Id
      });
    }

    /// <summary>
    /// Clear orders
    /// </summary>
    public virtual Task<StatusResponse> Clear()
    {
      State.Grains.Clear();

      return Task.FromResult(new StatusResponse
      {
        Data = StatusEnum.Inactive
      });
    }

    /// <summary>
    /// Convert hierarchy of orders into a plain list
    /// </summary>
    protected virtual List<Order> Compose(Order order)
    {
      var nextOrders = order
        .Orders
        .Where(o => o.Instruction is null)
        .Select(o => Merge(order, o))
        .ToList();

      if (order.Amount is not null || order.Orders.Count is 0)
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
    protected virtual Order Merge(Order group, Order order)
    {
      var instrument =
        order?.Operation?.Instrument ??
        group?.Operation?.Instrument;

      var groupOrders = order
        ?.Orders
        ?.Where(o => o.Instruction is InstructionEnum.Brace)
        ?.Select(o => Merge(group, o));

      var nextOrder = order with
      {
        Descriptor = group.Descriptor,
        Orders = [.. groupOrders],
        Operation = order.Operation with
        {
          Time = instrument?.Price?.Time
        }
      };

      return nextOrder;
    }

    /// <summary>
    /// Preprocess order
    /// </summary>
    /// <param name="order"></param>
    protected virtual List<Error> Errors(Order order)
    {
      var response = new List<Error>();
      var orders = order.Orders.Append(order);

      foreach (var subOrder in orders)
      {
        var errors = orderValidator
          .Validate(subOrder)
          .Errors
          .Select(error => new Error { Message = error.ErrorMessage });

        response.AddRange(errors);
      }

      return response;
    }
  }
}
