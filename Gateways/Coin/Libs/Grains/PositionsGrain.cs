using Coin.Models;
using Core.Enums;
using Core.Grains;
using Core.Models;
using CryptoClients.Net;
using CryptoClients.Net.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace Coin.Grains
{
  public interface ICoinPositionsGrain : IPositionsGrain
  {
    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    Task<StatusResponse> Setup(Connection connection);
  }

  public class CoinPositionsGrain : PositionsGrain, ICoinPositionsGrain
  {
    /// <summary>
    /// State
    /// </summary>
    protected Connection state;

    /// <summary>
    /// Connector
    /// </summary>
    protected IExchangeRestClient sender;

    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    public virtual async Task<StatusResponse> Setup(Connection connection)
    {
      var cleaner = new CancellationTokenSource(connection.Timeout);

      state = connection;
      sender = new ExchangeRestClient();

      return new()
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<OrdersResponse> Positions(Criteria criteria)
    {
      return new()
      {
        Data = []
      };
    }
  }
}
