using Core.Enums;
using Core.Models;
using Orleans;
using System.Threading.Tasks;

namespace Core.Grains
{
  public interface IOrderGrain : IGrainWithStringKey
  {
    /// <summary>
    /// Get order
    /// </summary>
    Task<OrderModel> Order();

    /// <summary>
    /// Get position
    /// </summary>
    /// <param name="price"></param>
    Task<OrderModel> Position(PriceModel price);

    /// <summary>
    /// Send order
    /// </summary>
    Task<OrderResponse> Store(OrderModel order);

    /// <summary>
    /// Check if pending order can be executed
    /// </summary>
    /// <param name="price"></param>
    Task<bool> IsExecutable(PriceModel price);
  }

  public class OrderGrain : Grain<OrderModel>, IOrderGrain
  {
    /// <summary>
    /// Get order 
    /// </summary>
    public virtual Task<OrderModel> Order() => Task.FromResult(State);

    /// <summary>
    /// Send order
    /// </summary>
    /// <param name="order"></param>
    public virtual Task<OrderResponse> Store(OrderModel order)
    {
      State = order with
      {
        Operation = order.Operation with
        {
          Status = OrderStatusEnum.Order
        }
      };

      return Task.FromResult(new OrderResponse
      {
        Data = State
      });
    }

    /// <summary>
    /// Get position
    /// </summary>
    /// <param name="price"></param>
    public virtual Task<OrderModel> Position(PriceModel price)
    {
      var position = State with
      {
        Price = price.Last,
        Operation = State.Operation with
        {
          Time = price.Time,
          Amount = State.Amount,
          AveragePrice = price.Last,
          Status = OrderStatusEnum.Position,
          Instrument = State.Operation.Instrument with
          {
            Price = price
          }
        }
      };

      return Task.FromResult(position);
    }

    /// <summary>
    /// Check if pending order can be executed
    /// </summary>
    /// <param name="price"></param>
    public virtual Task<bool> IsExecutable(PriceModel price)
    {
      var response = false;

      if (Equals(price.Name, State.Operation.Instrument.Name) is false)
      {
        return Task.FromResult(response);
      }

      var isLong = State.Side is OrderSideEnum.Long;
      var isShort = State.Side is OrderSideEnum.Short;

      if (State.Type is OrderTypeEnum.StopLimit)
      {
        var isLongLimit = isLong && price.Ask >= State.ActivationPrice;
        var isShortLimit = isShort && price.Bid <= State.ActivationPrice;

        if (isLongLimit || isShortLimit)
        {
          State = State with { Type = OrderTypeEnum.Limit };
        }
      }

      switch (State.Type)
      {
        case OrderTypeEnum.Market: response = true; break;
        case OrderTypeEnum.Stop: response = isLong ? price.Ask >= State.Price : price.Bid <= State.Price; break;
        case OrderTypeEnum.Limit: response = isLong ? price.Ask <= State.Price : price.Bid >= State.Price; break;
      }

      return Task.FromResult(response);
    }
  }
}
