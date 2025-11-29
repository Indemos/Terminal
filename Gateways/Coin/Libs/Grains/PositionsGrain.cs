using Coin.Models;
using Core.Enums;
using Core.Grains;
using Core.Models;
using CryptoClients.Net;
using CryptoClients.Net.Interfaces;
using CryptoExchange.Net.SharedApis;
using System.Linq;
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

      sender.SetApiCredentials(state.Exchange, state.Token, state.Secret);

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
      var response = new OrdersResponse();

      switch (true)
      {
        case true when Equals(state.Exchange, sender.Coinbase.Exchange): response = await Coinbase(criteria); break;
      }

      return response;
    }

    /// <summary>
    /// Get positions from Coinbase
    /// </summary>
    /// <param name="criteria"></param>
    protected virtual async Task<OrdersResponse> Coinbase(Criteria criteria)
    {
      var currency = criteria.Instrument.Currency.Name;
      var groupsCleaner = new CancellationTokenSource(state.Timeout);
      var groups = await sender.Coinbase.AdvancedTradeApi.Account.GetPortfoliosAsync(null, groupsCleaner.Token);

      foreach (var group in groups.Data)
      {
        var groupCleaner = new CancellationTokenSource(state.Timeout);
        var response = await sender.Coinbase.AdvancedTradeApi.Account.GetPortfolioAsync(group.Id, currency, groupCleaner.Token);
      }

      return new()
      {
        Data = []
      };
    }
  }
}
