using Core.Enums;
using Core.Extensions;
using Core.Grains;
using Core.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Simulation.Grains
{
  public interface ISimOrdersGrain : IOrdersGrain
  {
    /// <summary>
    /// Update order data
    /// </summary>
    /// <param name="instrument"></param>
    Task<StatusResponse> Tap(Instrument instrument);

    /// <summary>
    /// Send order
    /// </summary>
    /// <param name="order"></param>
    Task<OrderResponse> Send(Order order);
  }

  public class SimOrdersGrain : OrdersGrain, ISimOrdersGrain
  {
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
      var positionsGrain = GrainFactory.GetGrain<ISimPositionsGrain>(descriptor);

      foreach (var order in State)
      {
        var position = IsExecutable(order.Value, instrument);

        if (position is not null)
        {
          State.Remove(order.Key);
          await positionsGrain.Send(position);
        }
      }

      return new()
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Check if pending order can be executed
    /// </summary>
    /// <param name="order"></param>
    /// <param name="instrument"></param>
    protected virtual Order IsExecutable(Order order, Instrument instrument)
    {
      var price = instrument.Price;

      if (Equals(instrument.Name, order.Operation.Instrument.Name) is false)
      {
        return null;
      }

      var isLong = order.Side is OrderSideEnum.Long;
      var isShort = order.Side is OrderSideEnum.Short;

      if (order.Type is OrderTypeEnum.StopLimit)
      {
        var isLongLimit = isLong && price.Ask >= order.ActivationPrice;
        var isShortLimit = isShort && price.Bid <= order.ActivationPrice;

        if (isLongLimit || isShortLimit)
        {
          order = order with { Type = OrderTypeEnum.Limit };
        }
      }

      var status = false;

      switch (order.Type)
      {
        case OrderTypeEnum.Market: status = true; break;
        case OrderTypeEnum.Stop: status = isLong ? price.Ask >= order.Price : price.Bid <= order.Price; break;
        case OrderTypeEnum.Limit: status = isLong ? price.Ask <= order.Price : price.Bid >= order.Price; break;
      }

      if (status)
      {
        return Position(order, price);
      }

      return null;
    }

    /// <summary>
    /// Get position
    /// </summary>
    /// <param name="order"></param>
    /// <param name="price"></param>
    protected virtual Order Position(Order order, Price price)
    {
      var position = order with
      {
        Price = price.Last,
        Operation = order.Operation with
        {
          Id = order.Id,
          Time = price.Time,
          Amount = order.Amount,
          AveragePrice = price.Last,
          Status = OrderStatusEnum.Position,
          Instrument = order.Operation.Instrument with
          {
            Price = price
          }
        }
      };

      return position;
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
