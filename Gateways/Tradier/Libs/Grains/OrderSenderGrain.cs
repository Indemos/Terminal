using Core.Enums;
using Core.Models;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Tradier.Grains
{
  public interface ITradierOrderSenderGrain : ITradierOrdersGrain
  {
  }

  public class TradierOrderSenderGrain : TradierOrdersGrain, ITradierOrderSenderGrain
  {
    /// <summary>
    /// Send order
    /// </summary>
    /// <param name="order"></param>
    public override async Task<OrderResponse> Send(Order order)
    {
      //var message = MapOrder(order);
      //var accountCode = order.Account.Descriptor;
      //var messageResponse = await connector.SendOrder(message, accountCode, CancellationToken.None);
      //var response = new OrderResponse { Data = new() { Id = messageResponse.OrderId } };

      return null;
    }
  }
}
