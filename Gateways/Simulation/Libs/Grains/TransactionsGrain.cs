using Core.Conventions;
using Core.Enums;
using Core.Grains;
using Core.Models;
using Simulation.Models;
using System;
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

    /// <summary>
    /// Get transactions
    /// </summary>
    /// <param name="criteria"></param>
    public override Task<OrdersResponse> Transactions(TransactionCriteria criteria)
    {
      var count = Math.Min(criteria?.Count ?? State.Count, State.Count);

      return Task.FromResult(new OrdersResponse
      {
        Data = State.GetRange(State.Count - count, count)
      });
    }
  }
}
