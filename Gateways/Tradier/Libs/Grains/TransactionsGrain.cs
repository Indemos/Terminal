using Core.Conventions;
using Core.Enums;
using Core.Grains;
using Core.Models;
using System.Threading;
using System.Threading.Tasks;
using Tradier.Models;

namespace Tradier.Grains
{
  public interface ITradierTransactionsGrain : ITransactionsGrain
  {
    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="observer"></param>
    Task<StatusResponse> Setup(Connection connection, ITradeObserver observer);
  }

  public class TradierTransactionsGrain : TransactionsGrain, ITradierTransactionsGrain
  {
    /// <summary>
    /// State
    /// </summary>
    protected Connection state;

    /// <summary>
    /// Connector
    /// </summary>
    protected TradierBroker connector;

    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="grainObserver"></param>
    public virtual async Task<StatusResponse> Setup(Connection connection, ITradeObserver grainObserver)
    {
      var cleaner = new CancellationTokenSource(connection.Timeout);

      state = connection;
      observer = grainObserver;
      connector = new()
      {
        Token = connection.AccessToken,
        SessionToken = connection.SessionToken,
      };

      await connector.Connect(cleaner.Token);

      return new()
      {
        Data = StatusEnum.Active
      };
    }
  }
}
