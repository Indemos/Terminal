using Core.Enums;
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
    Task<IList<OrderModel>> Orders(CriteriaModel criteria);

    /// <summary>
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="order"></param>
    Task<OrderGroupsResponse> Store(OrderModel order);

    /// <summary>
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="price"></param>
    Task<StatusResponse> Tap(PriceModel price);

    /// <summary>
    /// Remove order from the list
    /// </summary>
    /// <param name="order"></param>
    Task<DescriptorResponse> Clear(OrderModel order);

    /// <summary>
    /// Clear orders
    /// </summary>
    Task<StatusResponse> Clear();
  }

  public class OrdersGrain : Grain<OrdersModel>, IOrdersGrain
  {
    /// <summary>
    /// Order validator
    /// </summary>
    protected OrderValidator orderValidator = new();

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    public virtual async Task<IList<OrderModel>> Orders(CriteriaModel criteria) => await Task.WhenAll(State
      .Grains
      .Values
      .Select(o => o.Order()));

    /// <summary>
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="order"></param>
    public virtual async Task<OrderGroupsResponse> Store(OrderModel order)
    {
      var orders = Compose(order);
      var descriptor = this.GetPrimaryKeyString();
      var responses = orders
        .Select(o => new OrderResponse { Data = o, Errors = [.. Errors(o).Select(error => error.Message)] })
        .ToArray();

      if (responses.Sum(o => o.Errors.Count) is 0)
      {
        foreach (var o in orders)
        {
          var name = $"{descriptor}:{o.Id}";
          var grain = GrainFactory.GetGrain<IOrderGrain>(name);

          await grain.Store(o);

          State.Grains[order.Id] = grain;
        }
      }

      return new()
      {
        Data = responses
      };
    }

    /// <summary>
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="price"></param>
    public virtual async Task<StatusResponse> Tap(PriceModel price)
    {
      var descriptor = this.GetPrimaryKeyString();
      var positionsGrain = GrainFactory.GetGrain<IPositionsGrain>(descriptor);

      foreach (var grain in State.Grains)
      {
        if (await grain.Value.IsExecutable(price))
        {
          State.Grains.Remove(grain.Key);
          await positionsGrain.Store(await grain.Value.Position(price));
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
    public virtual Task<DescriptorResponse> Clear(OrderModel order)
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
    protected virtual List<OrderModel> Compose(OrderModel order)
    {
      var nextOrders = order
        .Orders
        .Where(o => o.Instruction is null)
        .Select(o => Merge(o, order))
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
    protected virtual OrderModel Merge(OrderModel group, OrderModel order)
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
    protected virtual List<ErrorModel> Errors(OrderModel order)
    {
      var response = new List<ErrorModel>();
      var orders = order.Orders.Append(order);

      foreach (var subOrder in orders)
      {
        var errors = orderValidator
          .Validate(subOrder)
          .Errors
          .Select(error => new ErrorModel { Message = error.ErrorMessage });

        response.AddRange(errors);
      }

      return response;
    }
  }
}
