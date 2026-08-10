using Core.Conventions;
using Core.Enums;
using Core.Grains;
using Core.Models;
using Schwab.Models;
using System.Threading.Tasks;

namespace Schwab.Grains
{
  public interface ISchwabTransactionsGrain : ITransactionsGrain
  {
    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="grainObserver"></param>
    Task<StatusResponse> Setup(Connection connection, ITradeObserver grainObserver);
  }

  public class SchwabTransactionsGrain : TransactionsGrain, ISchwabTransactionsGrain
  {
    /// <summary>
    /// State
    /// </summary>
    protected Connection state;

    /// <summary>
    /// Connector
    /// </summary>
    protected SchwabBroker connector = new();

    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="grainObserver"></param>
    public virtual async Task<StatusResponse> Setup(Connection connection, ITradeObserver grainObserver)
    {
      state = connection;
      observer = grainObserver;
      connector.AccessToken = connection.AccessToken;

      return new()
      {
        Data = StatusEnum.Active
      };
    }
  }
}
