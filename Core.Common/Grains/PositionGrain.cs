using Core.Common.Enums;
using Core.Common.Extensions;
using Core.Common.Services;
using Core.Common.States;
using Orleans;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Common.Grains
{
  public interface IPositionGrain : IGrainWithStringKey
  {
    /// <summary>
    /// Get order state
    /// </summary>
    Task<OrderState> Position();

    /// <summary>
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="price"></param>
    Task Tap(PriceState price);

    /// <summary>
    /// Create position
    /// </summary>
    /// <param name="order"></param>
    Task Store(OrderState order);

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
    protected Descriptor descriptor;

    /// <summary>
    /// Activation
    /// </summary>
    /// <param name="cancellation"></param>
    public override async Task OnActivateAsync(CancellationToken cancellation)
    {
      descriptor = InstanceService<ConversionService>
        .Instance
        .Decompose<Descriptor>(this.GetPrimaryKeyString());

      await base.OnActivateAsync(cancellation);
    }

    /// <summary>
    /// Get order state
    /// </summary>
    public Task<OrderState> Position()
    {
      return Task.FromResult(State);
    }

    /// <summary>
    /// Create position
    /// </summary>
    /// <param name="order"></param>
    public async Task Store(OrderState order)
    {
      State = order;

      await SendBraces(order);
    }

    /// <summary>
    /// Match positions
    /// </summary>
    /// <param name="order"></param>
    public async Task<OrderResponse> Combine(OrderState order)
    {
      var response = new OrderResponse();

      if (Equals(State.Side, order.Side))
      {
        State = Increase(order);
      }
      else
      {
        var state = Decrease(order);

        if (order.Amount.IsGt(State.Operation.Amount))
        {
          state = Inverse(order);
        }

        response = response with { Data = State = state };
      }

      if (response.Data is not null)
      {
        await SendBraces(State, ActionEnum.Delete);
        await SendBraces(order);
      }

      return response;
    }

    /// <summary>
    /// Increase position
    /// </summary>
    /// <param name="order"></param>
    protected OrderState Increase(OrderState order)
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
    protected OrderState Decrease(OrderState order)
    {
      var amount = Math.Min(order.Amount.Value, State.Operation.Amount.Value);

      return State with
      {
        Amount = amount,
        Operation = State.Operation with
        {
          Amount = amount,
          Price = order.Price,
          Status = OrderStatusEnum.Transaction
        }
      };
    }

    /// <summary>
    /// Reverse position
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    protected OrderState Inverse(OrderState order)
    {
      var amount = order.Amount - State.Operation.Amount;

      return order with
      {
        Amount = amount,
        Operation = State.Operation with
        {
          Amount = amount
        }
      };
    }

    /// <summary>
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="price"></param>
    public Task Tap(PriceState price)
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
              Point = price
            }
          }
        };
      }

      return Task.CompletedTask;
    }

    /// <summary>
    /// Update SL and TP orders
    /// </summary>
    /// <param name="order"></param>
    /// <param name="close"></param>
    protected async Task SendBraces(OrderState order, ActionEnum action = ActionEnum.Create)
    {
      var ordersGrain = GrainFactory.Get<IOrdersGrain>(descriptor);

      foreach (var brace in order.Orders.Where(o => o.Instruction is InstructionEnum.Brace))
      {
        switch (action)
        {
          case ActionEnum.Create: await ordersGrain.Send(brace); break;
          case ActionEnum.Delete: await ordersGrain.Remove(brace); break;
        }
      }
    }

    /// <summary>
    /// Position direction
    /// </summary>
    protected double? Direction()
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
    protected double? Price()
    {
      var point = State.Operation.Instrument.Point;

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
    protected double Range()
    {
      var price = State.Operation.Price ?? Price();
      var estimate = (price - State.Operation.AveragePrice) * Direction();

      return estimate.Value;
    }

    /// <summary>
    /// Estimated PnL in account's currency for the order
    /// </summary>
    protected BalanceState Balance()
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
    protected double? GroupPrice(params OrderState[] orders)
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
