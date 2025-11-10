using Core.Models;
using Orleans;
using System.Threading.Tasks;

namespace Core.Grains
{
  public interface ITransactionGrain : IGrainWithStringKey
  {
    /// <summary>
    /// Get order state
    /// </summary>
    Task<Order> Transaction();

    /// <summary>
    /// Store order
    /// </summary>
    /// <param name="order"></param>
    Task<OrderResponse> Store(Order order);
  }

  public class TransactionGrain : Grain<Order>, ITransactionGrain
  {
    /// <summary>
    /// Get order state
    /// </summary>
    public virtual Task<Order> Transaction() => Task.FromResult(State);

    /// <summary>
    /// Store order
    /// </summary>
    /// <param name="order"></param>
    public virtual Task<OrderResponse> Store(Order order) => Task.FromResult(new OrderResponse
    {
      Data = State = order
    });
  }
}
