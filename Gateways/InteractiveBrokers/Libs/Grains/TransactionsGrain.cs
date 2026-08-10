using Core.Conventions;
using Core.Enums;
using Core.Grains;
using Core.Models;
using IBApi;
using InteractiveBrokers.Models;
using System.Threading.Tasks;

namespace InteractiveBrokers.Grains
{
  public interface IInterTransactionsGrain : ITransactionsGrain
  {
    /// <summary>
    /// Setup
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="grainObserver"></param>
    Task<StatusResponse> Setup(Connection connection, ITradeObserver grainObserver);
  }

  public class InterTransactionsGrain : TransactionsGrain, IInterTransactionsGrain
  {
    /// <summary>
    /// State
    /// </summary>
    protected Connection state;

    /// <summary>
    /// IB client
    /// </summary>
    protected InterBroker connector;

    /// <summary>
    /// Setup
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="grainObserver"></param>
    public virtual async Task<StatusResponse> Setup(Connection connection, ITradeObserver grainObserver)
    {
      state = connection;
      observer = grainObserver;

      connector?.Disconnect();
      connector = new InterBroker
      {
        Port = state.Port,
        Span = state.Span,
        Timeout = state.Timeout
      };

      await connector.Connect();

      return new()
      {
        Data = StatusEnum.Active
      };
    }
  }
}
