using Core.Common.States;
using Orleans;
using System.Threading.Tasks;

namespace Core.Common.Grains
{
  public class TransactionGrain : Grain<OrderState>, IGrainWithStringKey
  {
    /// <summary>
    /// Get order state
    /// </summary>
    public Task<OrderState> Get()
    {
      return Task.FromResult(State);
    }
  }
}
