using Core.Enums;
using Core.Grains;
using Core.Models;
using Schwab.Models;
using System.Threading.Tasks;

namespace Schwab.Grains
{
  public interface ISchwabPositionsGrain : IPositionsGrain
  {
    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    Task<StatusResponse> Connect(ConnectionModel connection);
  }

  public class SchwabPositionsGrain : PositionsGrain, ISchwabPositionsGrain
  {
    /// <summary>
    /// Messenger
    /// </summary>
    protected SchwabBroker broker;

    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    public virtual async Task<StatusResponse> Connect(ConnectionModel connection)
    {
      broker = new()
      {
        ClientId = connection.Id,
        ClientSecret = connection.Secret,
        AccessToken = connection.AccessToken,
        RefreshToken = connection.RefreshToken
      };

      await broker.Connect();

      return new()
      {
        Data = StatusEnum.Active
      };
    }
  }
}
