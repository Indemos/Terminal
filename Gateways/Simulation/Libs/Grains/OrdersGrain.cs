using Core.Enums;
using Core.Extensions;
using Core.Grains;
using Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Simulation.Grains
{
  public interface ISimOrdersGrain : IOrdersGrain
  {
    /// <summary>
    /// Update order
    /// </summary>
    /// <param name="instrument"></param>
    /// <param name="order"></param>
    Task<DescriptorResponse> Tap(Instrument instrument, Order order);

    /// <summary>
    /// Update orders
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
    /// Store order
    /// </summary>
    /// <param name="order"></param>
    public override async Task<OrderResponse> Store(Order order)
    {
      var response = Order(order, order.Operation.Instrument);

      State[response.Id] = response;

      return new()
      {
        Data = response
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
    /// Update orders
    /// </summary>
    /// <param name="instrument"></param>
    public virtual async Task<StatusResponse> Tap(Instrument instrument)
    {
      foreach (var order in State.Values)
      {
        await Tap(instrument, order);
      }

      return new()
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Update orders
    /// </summary>
    /// <param name="instrument"></param>
    /// <param name="order"></param>
    public virtual async Task<DescriptorResponse> Tap(Instrument instrument, Order order)
    {
      var descriptor = this.GetDescriptor();
      var positionsGrain = GrainFactory.GetGrain<ISimPositionsGrain>(descriptor);
      var position = Process(order, instrument);

      if (position is not null)
      {
        State.Remove(order.Id);
        await positionsGrain.Send(position);
      }

      return new()
      {
        Data = order.Id
      };
    }

    /// <summary>
    /// Check if pending order can be executed
    /// </summary>
    /// <param name="order"></param>
    /// <param name="instrument"></param>
    protected virtual Order Process(Order order, Instrument instrument)
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
        return Position(order, instrument);
      }

      return null;
    }

    /// <summary>
    /// Store order
    /// </summary>
    /// <param name="order"></param>
    /// <param name="instrument"></param>
    protected virtual Order Order(Order order, Instrument instrument)
    {
      var response = order with
      {
        Id = $"{Guid.NewGuid()}",
        Operation = order.Operation with
        {
          Status = OrderStatusEnum.Order
        }
      };

      if (instrument?.Price is not null)
      {
        var price = Price(order, instrument);

        return response with
        {
          Time = instrument.Price.Time,
          Price = order.Price ?? price
        };
      }

      return response;
    }

    /// <summary>
    /// Get position
    /// </summary>
    /// <param name="order"></param>
    /// <param name="instrument"></param>
    protected virtual Order Position(Order order, Instrument instrument)
    {
      var price = Price(order, instrument);
      var position = order with
      {
        Price = order.Price ?? price,
        Operation = order.Operation with
        {
          AveragePrice = price,
          Amount = order.Amount,
          Time = instrument.Price.Time,
          Status = OrderStatusEnum.Position,
          Instrument = order.Operation.Instrument with
          {
            Price = instrument.Price
          }
        }
      };

      return position;
    }

    /// <summary>
    /// Derive open price
    /// </summary>
    /// <param name="order"></param>
    /// <param name="instrument"></param>
    protected virtual double? Price(Order order, Instrument instrument)
    {
      var bid = instrument.Price.Bid.Value;
      var ask = instrument.Price.Ask.Value;
      var isLong = order.Side is OrderSideEnum.Long;

      switch (true)
      {
        case true when order.Type is OrderTypeEnum.Stop or OrderTypeEnum.Market or null: return isLong ? ask : bid;
        case true when order.Type is OrderTypeEnum.Limit:
          return isLong ?
          Math.Min(order.Price ?? ask, ask) :
          Math.Max(order.Price ?? bid, bid);
      }

      return null;
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
