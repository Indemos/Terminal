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
    /// Stamp
    /// </summary>
    /// <param name="accessToken"></param>
    Task<StatusResponse> Stamp(string accessToken);

    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="observer"></param>
    Task<StatusResponse> Setup(Connection connection, ITradeObserver observer);
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
    /// Stamp
    /// </summary>
    /// <param name="accessToken"></param>
    public virtual async Task<StatusResponse> Stamp(string accessToken)
    {
      connector.AccessToken = accessToken;

      return new()
      {
        Data = StatusEnum.Active
      };
    }

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
