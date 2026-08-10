using Core.Enums;
using Core.Models;
using System.Threading.Tasks;
using TopstepX.Models.Orders;

namespace Topstep.Grains
{
  public interface ITopstepOrderSenderGrain : ITopstepOrdersGrain
  {
    /// <summary>
    /// Send order
    /// </summary>
    /// <param name="order"></param>
    Task<OrderResponse> Send(Order order);
  }

  public class TopstepOrderSenderGrain : TopstepOrdersGrain, ITopstepOrderSenderGrain
  {
    /// <summary>
    /// Send order
    /// </summary>
    /// <param name="order"></param>
    public virtual async Task<OrderResponse> Send(Order order)
    {
      var orderMessage = MapOrder(order, state.Account);
      var orderResponse = await connector.OrderPlace(orderMessage);

      order = order with { Operation = order.Operation with { Id = $"{orderResponse.orderId}" } };

      return new()
      {
        Data = order
      };
    }

    /// <summary>
    /// Map order
    /// </summary>
    /// <param name="order"></param>
    /// <param name="account"></param>
    protected virtual PlaceOrderRequest MapOrder(Order order, Account account)
    {
      var response = new PlaceOrderRequest
      {
        size = 1,
        contractId = order.Operation.Instrument.Id,
        accountId = int.Parse(account.Descriptor),
        side = MapSide(order).Value,
        type = MapType(order)
      };

      switch (order.Type)
      {
        case OrderTypeEnum.Stop: response.stopPrice = order.Price; break;
        case OrderTypeEnum.Limit: response.limitPrice = order.Price; break;
        case OrderTypeEnum.StopLimit:
          response.limitPrice = order.Price;
          response.stopPrice = order.ActivationPrice;
          break;
      }

      return response;
    }

    /// <summary>
    /// Order side
    /// </summary>
    /// <param name="order"></param>
    protected virtual OrderSide? MapSide(Order order)
    {
      switch (order.Side)
      {
        case OrderSideEnum.Long: return OrderSide.Buy;
        case OrderSideEnum.Short: return OrderSide.Sell;
      }

      return null;
    }

    /// <summary>
    /// Order type
    /// </summary>
    /// <param name="order"></param>
    protected virtual OrderType MapType(Order order)
    {
      switch (order.Type)
      {
        case OrderTypeEnum.Stop: return OrderType.Stop;
        case OrderTypeEnum.Limit: return OrderType.Limit;
        case OrderTypeEnum.StopLimit: return OrderType.StopLimit;
      }

      return OrderType.Market;
    }
  }
}
