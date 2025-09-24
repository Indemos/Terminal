using Core.Common.Enums;
using Core.Common.Extensions;
using Core.Common.Services;
using Core.Common.States;
using Orleans;
using Orleans.Serialization.Invocation;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Common.Grains
{
  public interface IPositionGrain : IGrainWithStringKey
  {
    /// <summary>
    /// Get position
    /// </summary>
    Task<OrderState> Position();

    /// <summary>
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="price"></param>
    Task<StatusResponse> Tap(PriceState price);

    /// <summary>
    /// Create position
    /// </summary>
    /// <param name="order"></param>
    Task<OrderResponse> Store(OrderState order);

    /// <summary>
    /// Match positions
    /// </summary>
    /// <param name="order"></param>
    Task<OrderResponse> Combine(OrderState order);
  }

  public class PositionGrain : Grain<OrderState>, IPositionGrain
  {
    /// <summary>
    /// Descriptor
    /// </summary>
    protected DescriptorState descriptor;

    /// <summary>
    /// Activation
    /// </summary>
    /// <param name="cleaner"></param>
    public override async Task OnActivateAsync(CancellationToken cleaner)
    {
      descriptor = InstanceService<ConversionService>
        .Instance
        .Decompose<DescriptorState>(this.GetPrimaryKeyString());

      await base.OnActivateAsync(cleaner);
    }

    /// <summary>
    /// Get position
    /// </summary>
    public virtual Task<OrderState> Position() => Task.FromResult(State);

    /// <summary>
    /// Create position
    /// </summary>
    /// <param name="order"></param>
    public virtual async Task<OrderResponse> Store(OrderState order)
    {
      await SendBraces(State = order);

      return new()
      {
        Data = order
      };
    }

    /// <summary>
    /// Match positions
    /// </summary>
    /// <param name="order"></param>
    public virtual async Task<OrderResponse> Combine(OrderState order)
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
    /// <param name="price"></param>
    public virtual Task<StatusResponse> Tap(PriceState price)
    {
      if (Equals(price.Name, State.Operation.Instrument.Name))
      {
        State = State with
        {
          Balance = Balance(),
          Operation = State.Operation with
          {
            Instrument = State.Operation.Instrument with
            {
              Price = price
            }
          }
        };
      }

      return Task.FromResult(new StatusResponse
      {
        Data = StatusEnum.Active
      });
    }

    /// <summary>
    /// Increase position
    /// </summary>
    /// <param name="order"></param>
    protected virtual OrderState Increase(OrderState order)
    {
      var amount = State.Operation.Amount + order.Amount;

      return State with
      {
        Amount = amount,
        Operation = State.Operation with
        {
          Amount = amount,
          AveragePrice = GroupPrice(State, order)
        }
      };
    }

    /// <summary>
    /// Decrease position by amount or close 
    /// </summary>
    /// <param name="order"></param>
    protected virtual OrderResponse Decrease(OrderState order)
    {
      var action = State with
      {
        Amount = order.Amount,
        Operation = State.Operation with
        {
          Price = order.Price,
          Amount = order.Amount,
          Status = OrderStatusEnum.Transaction
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
    protected virtual OrderResponse Inverse(OrderState order)
    {
      var action = State with
      {
        Operation = State.Operation with
        {
          Price = order.Price,
          Status = OrderStatusEnum.Transaction
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
    protected virtual async Task SendBraces(OrderState order, ActionEnum action = ActionEnum.Create)
    {
      var ordersGrain = GrainFactory.Get<IOrdersGrain>(descriptor);

      foreach (var brace in order.Orders.Where(o => o.Instruction is InstructionEnum.Brace))
      {
        switch (action)
        {
          case ActionEnum.Create: await ordersGrain.Store(brace); break;
          case ActionEnum.Remove: await ordersGrain.Remove(brace); break;
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
    protected virtual BalanceState Balance()
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
    protected virtual double? GroupPrice(params OrderState[] orders)
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
