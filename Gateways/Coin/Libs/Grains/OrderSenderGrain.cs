using Core.Models;
using System.Threading.Tasks;

namespace Coin.Grains
{
  public interface ICoinOrderSenderGrain : ICoinOrdersGrain
  {
    /// <summary>
    /// Send order
    /// </summary>
    /// <param name="order"></param>
    Task<OrderResponse> Send(Order order);
  }

  public class CoinOrderSenderGrain : CoinOrdersGrain, ICoinOrderSenderGrain
  {
    /// <summary>
    /// Send order
    /// </summary>
    /// <param name="order"></param>
    public virtual async Task<OrderResponse> Send(Order order)
    {
      return new()
      {
        Data = order
      };
    }
  }
}
