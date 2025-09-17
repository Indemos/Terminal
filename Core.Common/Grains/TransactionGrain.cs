using Core.Common.States;
using Orleans;
using System.Threading.Tasks;

namespace Core.Common.Grains
{
  public interface ITransactionGrain : IGrainWithStringKey
  {
    /// <summary>
    /// Get order state
    /// </summary>
    Task<OrderState> Transaction();

    /// <summary>
    /// Store order
    /// </summary>
    /// <param name="order"></param>
    Task<DescriptorResponse> Store(OrderState order);
  }

  public class TransactionGrain : Grain<OrderState>, ITransactionGrain
  {
    /// <summary>
    /// Get order state
    /// </summary>
    public virtual Task<OrderState> Transaction() => Task.FromResult(State);

    /// <summary>
    /// Store order
    /// </summary>
    /// <param name="order"></param>
    public virtual Task<DescriptorResponse> Store(OrderState order) => Task.FromResult(new DescriptorResponse
    {
      Data = (State = order).Id
    });
  }
}
