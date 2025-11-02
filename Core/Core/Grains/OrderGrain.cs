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
    /// Send order
    /// </summary>
    Task<OrderResponse> Store(OrderModel order);

    /// <summary>
    /// Check if pending order can be executed
    /// </summary>
    /// <param name="instrument"></param>
    Task<OrderResponse> Tap(InstrumentModel instrument);
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
    /// Check if pending order can be executed
    /// </summary>
    /// <param name="instrument"></param>
    public virtual Task<OrderResponse> Tap(InstrumentModel instrument)
    {
      var price = instrument.Price;
      var response = new OrderResponse();

      if (Equals(instrument.Name, State.Operation.Instrument.Name) is false)
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

      var status = false;

      switch (State.Type)
      {
        case OrderTypeEnum.Market: status = true; break;
        case OrderTypeEnum.Stop: status = isLong ? price.Ask >= State.Price : price.Bid <= State.Price; break;
        case OrderTypeEnum.Limit: status = isLong ? price.Ask <= State.Price : price.Bid >= State.Price; break;
      }

      if (status)
      {
        response = response with { Data = Position(price) };
      }

      return Task.FromResult(response);
    }

    /// <summary>
    /// Get position
    /// </summary>
    /// <param name="price"></param>
    protected virtual OrderModel Position(PriceModel price)
    {
      var position = State with
      {
        Price = price.Last,
        Operation = State.Operation with
        {
          Id = State.Id,
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

      return position;
    }
  }
}
