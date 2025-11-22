using Core.Enums;
using Core.Extensions;
using Core.Models;
using Core.Validators;
using Orleans;
using System;
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
    /// Store order
    /// </summary>
    /// <param name="order"></param>
    Task<OrderResponse> Store(Order order);

    /// <summary>
    /// Send order
    /// </summary>
    /// <param name="order"></param>
    Task<OrderResponse> Send(Order order);

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
    /// Store order
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
    public virtual async Task<OrderResponse> Send(Order order)
    {
      var response = new OrderResponse
      {
        Errors = [.. Errors(order).Select(error => error.Message).Distinct()]
      };

      if (response.Errors.Count is 0)
      {
        var orders = order
          .Orders
          .Where(o => o.Instruction is null)
          .ToList();

        if (order.Amount is not null || order.Orders.Count is 0)
        {
          orders.Add(order);
        }

        foreach (var o in orders)
        {
          await Store(o with { Orders = [.. o.Orders.Where(v => v.Instruction is InstructionEnum.Brace)] });
        }
      }

      return response;
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
          await positionsGrain.Send(response.Data);
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
