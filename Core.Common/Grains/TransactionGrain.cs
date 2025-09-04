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
    Task Store(OrderState order);
  }

  public class TransactionGrain : Grain<OrderState>, ITransactionGrain
  {
    /// <summary>
    /// Get order state
    /// </summary>
    public Task<OrderState> Transaction()
    {
      return Task.FromResult(State);
    }

    /// <summary>
    /// Store order
    /// </summary>
    public Task Store(OrderState order)
    {
      State = order;

      return Task.CompletedTask;
    }
  }
}
