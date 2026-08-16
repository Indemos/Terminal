using Core.Conventions;
using Core.Enums;
using Core.Grains;
using Core.Models;
using Simulation.Models;
using System.Threading.Tasks;

namespace Simulation.Grains
{
  public interface ISimTransactionsGrain : ITransactionsGrain
  {
    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="grainObserver"></param>
    Task<StatusResponse> Setup(Connection connection, ITradeObserver grainObserver);
  }

  public class SimTransactionsGrain : TransactionsGrain, ISimTransactionsGrain
  {
    /// <summary>
    /// State
    /// </summary>
    protected Connection state;

    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="grainObserver"></param>
    public virtual async Task<StatusResponse> Setup(Connection connection, ITradeObserver grainObserver)
    {
      state = connection;
      observer = grainObserver;

      return new()
      {
        Data = StatusEnum.Active
      };
    }
  }
}
