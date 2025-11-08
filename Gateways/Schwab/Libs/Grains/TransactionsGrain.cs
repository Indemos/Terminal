using Core.Enums;
using Core.Grains;
using Core.Models;
using Core.Services;
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
    Task<StatusResponse> Setup(ConnectionModel connection);
  }

  public class SchwabTransactionsGrain(MessageService messenger) : TransactionsGrain(messenger), ISchwabTransactionsGrain
  {
    /// <summary>
    /// Messenger
    /// </summary>
    protected SchwabBroker broker;

    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    public virtual async Task<StatusResponse> Setup(ConnectionModel connection)
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
