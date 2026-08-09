using Core.Conventions;
using Core.Enums;
using Core.Grains;
using Core.Models;
using System.Threading.Tasks;
using Topstep.Models;
using TopstepX;

namespace Topstep.Grains
{
  public interface ITopstepTransactionsGrain : ITransactionsGrain
  {
    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="observer"></param>
    Task<StatusResponse> Setup(Connection connection, ITradeObserver observer);

    /// <summary>
    /// Validate session token
    /// </summary>
    /// <param name="session"></param>
    Task<StatusResponse> Validate(string session);
  }

  public class TopstepTransactionsGrain : TransactionsGrain, ITopstepTransactionsGrain
  {
    /// <summary>
    /// State
    /// </summary>
    protected Connection state;

    /// <summary>
    /// Connector
    /// </summary>
    protected TopstepBroker connector;

    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="grainObserver"></param>
    public virtual async Task<StatusResponse> Setup(Connection connection, ITradeObserver grainObserver)
    {
      var response = new StatusResponse() { Data = StatusEnum.Active };

      state = connection;
      observer = grainObserver;
      connector = new(connection.Username, connection.Token);

      return response;
    }

    /// <summary>
    /// Validate session token
    /// </summary>
    /// <param name="session"></param>
    public virtual Task<StatusResponse> Validate(string session)
    {
      connector.SetAuthHeader(session);

      return Task.FromResult(new StatusResponse
      {
        Data = StatusEnum.Active
      });
    }
  }
}
