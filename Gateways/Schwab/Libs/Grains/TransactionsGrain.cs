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
    Task<StatusResponse> Setup(Connection connection);
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
    protected SchwabBroker connector;

    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    public virtual async Task<StatusResponse> Setup(Connection connection)
    {
      state = connection;
      connector = new()
      {
        ClientId = connection.Id,
        ClientSecret = connection.Secret,
        AccessToken = connection.AccessToken,
        RefreshToken = connection.RefreshToken
      };

      await connector.Connect();

      return new()
      {
        Data = StatusEnum.Active
      };
    }
  }
}
