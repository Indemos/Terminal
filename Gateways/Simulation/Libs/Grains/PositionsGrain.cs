using Core.Enums;
using Core.Extensions;
using Core.Grains;
using Core.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Simulation.Grains
{
  public interface ISimPositionsGrain : IPositionsGrain
  {
    /// <summary>
    /// Process order to position conversion
    /// </summary>
    /// <param name="order"></param>
    Task<OrderResponse> Send(Order order);

    /// <summary>
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="instrument"></param>
    Task<StatusResponse> Tap(Instrument instrument);
  }

  public class SimPositionsGrain : PositionsGrain, ISimPositionsGrain
  {
    /// <summary>
    /// Process order to position conversion
    /// </summary>
    /// <param name="order"></param>
    public virtual async Task<OrderResponse> Send(Order order)
    {
      var descriptor = this.GetDescriptor();
      var instrument = order.Operation.Instrument.Name;
      var currentOrder = State.Get(instrument);

      if (currentOrder is null)
      {
        await SendBraces(order);
        return await Store(order);
      }

      var response = await Combine(currentOrder, order);

      switch (response.Data is null)
      {
        case true: State.Remove(instrument); break;
        case false: State[instrument] = response.Data; break;
      }

      if (response.Transaction is not null)
      {
        await GrainFactory
          .GetGrain<ITransactionsGrain>(descriptor)
          .Store(response.Transaction);
      }

      return response;
    }

    /// <summary>
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="instrument"></param>
    public virtual async Task<StatusResponse> Tap(Instrument instrument)
    {
      foreach (var name in State.Keys)
      {
        if (Equals(name, instrument.Name))
        {
          var order = State.Get(name) with
          {
            Operation = State.Get(name).Operation with
            {
              Instrument = instrument with { Basis = State.Get(name).Operation.Instrument.Basis }
            }
          };

          State[name] = order with
          {
            Balance = Balance(order)
          };
        }
      }

      return new StatusResponse
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Match positions
    /// </summary>
    /// <param name="nextOrder"></param>
    public virtual async Task<OrderResponse> Combine(Order order, Order nextOrder)
    {
      var response = new OrderResponse();

      if (Equals(order.Side, nextOrder.Side))
      {
        response = response with { Data = order = Increase(order, nextOrder) };
      }
      else
      {
        response = nextOrder.Amount.IsGt(order.Operation.Amount) ?
          Inverse(order, nextOrder) :
          Decrease(order, nextOrder);
      }

      if (nextOrder.Orders.Count is not 0)
      {
        await SendBraces(order, ActionEnum.Remove);
        await SendBraces(nextOrder);
      }

      return response;
    }

    /// <summary>
    /// Increase position
    /// </summary>
    /// <param name="order"></param>
    /// <param name="nextOrder"></param>
    protected virtual Order Increase(Order order, Order nextOrder)
    {
      var amount = order.Operation.Amount + nextOrder.Amount;
      var price = GroupPrice(order, nextOrder);

      return order with
      {
        Price = price,
        Amount = amount,
        Operation = order.Operation with
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
    /// <param name="nextOrder"></param>
    protected virtual OrderResponse Decrease(Order order, Order nextOrder)
    {
      var action = order with
      {
        Amount = nextOrder.Amount,
        Operation = order.Operation with
        {
          Id = nextOrder.Id,
          Price = nextOrder.Price,
          Amount = nextOrder.Amount,
          Status = OrderStatusEnum.Transaction,
          Time = nextOrder.Operation.Instrument.Price.Time
        }
      };

      var state = nextOrder.Amount.Is(order.Operation.Amount) ? null : order with
      {
        Amount = order.Amount - nextOrder.Amount,
        Operation = order.Operation with
        {
          Amount = order.Operation.Amount - nextOrder.Amount
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
    /// <param name="nextOrder"></param>
    protected virtual OrderResponse Inverse(Order order, Order nextOrder)
    {
      var action = order with
      {
        Operation = order.Operation with
        {
          Id = nextOrder.Id,
          Price = nextOrder.Price,
          Status = OrderStatusEnum.Transaction,
          Time = nextOrder.Operation.Instrument.Price.Time
        }
      };

      var state = nextOrder with
      {
        Amount = nextOrder.Amount - order.Operation.Amount,
        Operation = order.Operation with
        {
          AveragePrice = nextOrder.Price,
          Amount = nextOrder.Amount - order.Operation.Amount
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
      var ordersGrain = GrainFactory.GetGrain<ISimOrdersGrain>(descriptor);

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
    /// <param name="order"></param>
    protected virtual double? Direction(Order order)
    {
      switch (order.Side)
      {
        case OrderSideEnum.Long: return 1;
        case OrderSideEnum.Short: return -1;
      }

      return null;
    }

    /// <summary>
    /// Estimate close price for one of the instruments in the order
    /// </summary>
    /// <param name="order"></param>
    protected virtual double? Price(Order order)
    {
      var price = order.Operation.Instrument.Price;

      if (price is not null)
      {
        switch (order.Side)
        {
          case OrderSideEnum.Long: return price.Bid;
          case OrderSideEnum.Short: return price.Ask;
        }
      }

      return null;
    }

    /// <summary>
    /// Estimated PnL in points for one side of the order
    /// </summary>
    /// <param name="order"></param>
    protected virtual double Range(Order order)
    {
      var price = order.Operation.Price ?? Price(order);
      var estimate = (price - order.Operation.AveragePrice) * Direction(order);

      return estimate.Value;
    }

    /// <summary>
    /// Estimated PnL in account's currency for the order
    /// </summary>
    /// <param name="order"></param>
    protected virtual Balance Balance(Order order)
    {
      var range = Range(order);
      var amount = order.Operation.Amount;
      var instrument = order.Operation.Instrument;
      var estimate = amount * range * instrument.Leverage - instrument.Commission;
      var balance = estimate ?? order.Balance.Current ?? 0;

      return order.Balance with
      {
        Current = balance,
        Min = Math.Min(order.Balance.Min ?? 0, balance),
        Max = Math.Max(order.Balance.Max ?? 0, balance)
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
