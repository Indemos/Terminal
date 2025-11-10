using Core.Enums;
using Core.Extensions;
using Core.Models;
using Orleans;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Grains
{
  public interface IPositionGrain : IGrainWithStringKey
  {
    /// <summary>
    /// Get position
    /// </summary>
    Task<Order> Position();

    /// <summary>
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="instrument"></param>
    Task<StatusResponse> Tap(Instrument instrument);

    /// <summary>
    /// Create position
    /// </summary>
    /// <param name="order"></param>
    Task<OrderResponse> Store(Order order);

    /// <summary>
    /// Match positions
    /// </summary>
    /// <param name="order"></param>
    Task<OrderResponse> Combine(Order order);
  }

  public class PositionGrain : Grain<Order>, IPositionGrain
  {
    /// <summary>
    /// Get position
    /// </summary>
    public virtual Task<Order> Position() => Task.FromResult(State);

    /// <summary>
    /// Create position
    /// </summary>
    /// <param name="order"></param>
    public virtual async Task<OrderResponse> Store(Order order)
    {
      await SendBraces(State = order);

      return new()
      {
        Data = State
      };
    }

    /// <summary>
    /// Match positions
    /// </summary>
    /// <param name="order"></param>
    public virtual async Task<OrderResponse> Combine(Order order)
    {
      var response = new OrderResponse();

      if (Equals(State.Side, order.Side))
      {
        response = response with { Data = State = Increase(order) };
      }
      else
      {
        response = order.Amount.IsGt(State.Operation.Amount) ?
          Inverse(order) :
          Decrease(order);

        State = response.Data;
      }

      if (order.Orders.Count is not 0)
      {
        await SendBraces(State, ActionEnum.Remove);
        await SendBraces(order);
      }

      return response;
    }

    /// <summary>
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="instrument"></param>
    public virtual Task<StatusResponse> Tap(Instrument instrument)
    {
      State = State with
      {
        Balance = Balance(),
        Operation = State.Operation with
        {
          Instrument = instrument with { Basis = State.Operation.Instrument.Basis }
        }
      };

      return Task.FromResult(new StatusResponse
      {
        Data = StatusEnum.Active
      });
    }

    /// <summary>
    /// Increase position
    /// </summary>
    /// <param name="order"></param>
    protected virtual Order Increase(Order order)
    {
      var amount = State.Operation.Amount + order.Amount;
      var price = GroupPrice(State, order);

      return State with
      {
        Price = price,
        Amount = amount,
        Operation = State.Operation with
        {
          Amount = amount,
          AveragePrice = price
        }
      };
    }

    /// <summary>
    /// Decrease position by amount or close 
    /// </summary>
    /// <param name="order"></param>
    protected virtual OrderResponse Decrease(Order order)
    {
      var action = State with
      {
        Amount = order.Amount,
        Operation = State.Operation with
        {
          Id = order.Id,
          Price = order.Price,
          Amount = order.Amount,
          Status = OrderStatusEnum.Transaction,
          Time = order.Operation.Instrument.Price.Time
        }
      };

      var state = order.Amount.Is(State.Operation.Amount) ? null : State with
      {
        Amount = State.Amount - order.Amount,
        Operation = State.Operation with
        {
          Amount = State.Operation.Amount - order.Amount
        }
      };

      return new()
      {
        Data = state,
        Transaction = action
      };
    }

    /// <summary>
    /// Reverse position
    /// </summary>
    /// <param name="order"></param>
    protected virtual OrderResponse Inverse(Order order)
    {
      var action = State with
      {
        Operation = State.Operation with
        {
          Id = order.Id,
          Price = order.Price,
          Status = OrderStatusEnum.Transaction,
          Time = order.Operation.Instrument.Price.Time
        }
      };

      var state = order with
      {
        Amount = order.Amount - State.Operation.Amount,
        Operation = State.Operation with
        {
          AveragePrice = order.Price,
          Amount = order.Amount - State.Operation.Amount
        }
      };

      return new()
      {
        Data = state,
        Transaction = action
      };
    }

    /// <summary>
    /// Update SL and TP orders
    /// </summary>
    /// <param name="order"></param>
    /// <param name="action"></param>
    protected virtual async Task SendBraces(Order order, ActionEnum action = ActionEnum.Create)
    {
      var descriptor = this.GetDescriptor();
      var ordersGrain = GrainFactory.GetGrain<IOrdersGrain>(descriptor);

      foreach (var brace in order.Orders.Where(o => o.Instruction is InstructionEnum.Brace))
      {
        switch (action)
        {
          case ActionEnum.Create: await ordersGrain.Send(brace); break;
          case ActionEnum.Remove: await ordersGrain.Clear(brace); break;
        }
      }
    }

    /// <summary>
    /// Position direction
    /// </summary>
    protected virtual double? Direction()
    {
      switch (State.Side)
      {
        case OrderSideEnum.Long: return 1;
        case OrderSideEnum.Short: return -1;
      }

      return null;
    }

    /// <summary>
    /// Estimate close price for one of the instruments in the order
    /// </summary>
    protected virtual double? Price()
    {
      var point = State.Operation.Instrument.Price;

      if (point is not null)
      {
        switch (State.Side)
        {
          case OrderSideEnum.Long: return point.Bid;
          case OrderSideEnum.Short: return point.Ask;
        }
      }

      return null;
    }

    /// <summary>
    /// Estimated PnL in points for one side of the order
    /// </summary>
    protected virtual double Range()
    {
      var price = State.Operation.Price ?? Price();
      var estimate = (price - State.Operation.AveragePrice) * Direction();

      return estimate.Value;
    }

    /// <summary>
    /// Estimated PnL in account's currency for the order
    /// </summary>
    protected virtual Balance Balance()
    {
      var range = Range();
      var amount = State.Operation.Amount;
      var instrument = State.Operation.Instrument;
      var estimate = amount * range * instrument.Leverage - instrument.Commission;
      var balance = estimate ?? State.Balance.Current ?? 0;

      return State.Balance with
      {
        Current = balance,
        Min = Math.Min(State.Balance.Min ?? 0, balance),
        Max = Math.Max(State.Balance.Max ?? 0, balance)
      };
    }

    /// <summary>
    /// Compute aggregated position price
    /// </summary>
    /// <param name="orders"></param>
    protected virtual double? GroupPrice(params Order[] orders)
    {
      var numerator = 0.0 as double?;
      var denominator = 0.0 as double?;

      foreach (var o in orders)
      {
        switch (true)
        {
          case true when o.Operation.AveragePrice is null:
            numerator += o.Amount * o.Price;
            denominator += o.Amount;
            break;

          case true when o.Operation.AveragePrice is not null:
            numerator += o.Operation.Amount * o.Operation.AveragePrice;
            denominator += o.Operation.Amount;
            break;
        }
      }

      return numerator / denominator;
    }
  }
}
