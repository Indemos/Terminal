using Core.Common.Models;
using Orleans;
using System.Threading.Tasks;

namespace Core.Common.Grains
{
  public interface ITransactionGrain : IGrainWithStringKey
  {
    /// <summary>
    /// Get order state
    /// </summary>
    Task<OrderModel> Transaction();

    /// <summary>
    /// Store order
    /// </summary>
    /// <param name="order"></param>
    Task<OrderResponse> Store(OrderModel order);
  }

  public class TransactionGrain : Grain<OrderModel>, ITransactionGrain
  {
    /// <summary>
    /// Get order state
    /// </summary>
    public virtual Task<OrderModel> Transaction() => Task.FromResult(State);

    /// <summary>
    /// Store order
    /// </summary>
    /// <param name="order"></param>
    public virtual Task<OrderResponse> Store(OrderModel order) => Task.FromResult(new OrderResponse
    {
      Data = State = order
    });
  }
}
