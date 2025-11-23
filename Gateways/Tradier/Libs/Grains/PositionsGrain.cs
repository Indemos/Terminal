using Core.Enums;
using Core.Grains;
using Core.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tradier.Messages.Account;
using Tradier.Models;

namespace Tradier.Grains
{
  public interface ITradierPositionsGrain : IPositionsGrain
  {
    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    Task<StatusResponse> Setup(Connection connection);
  }

  public class TradierPositionsGrain : PositionsGrain, ITradierPositionsGrain
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
    public virtual async Task<StatusResponse> Setup(Connection connection)
    {
      var cleaner = new CancellationTokenSource(connection.Timeout);

      state = connection;
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

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<OrdersResponse> Positions(Criteria criteria)
    {
      var cleaner = new CancellationTokenSource(state.Timeout);
      var query = new Queries.Account.AccountRequest { AccountNumber = criteria.Account.Descriptor };
      var messages = await connector.GetPositions(query, cleaner.Token);
      var items = messages.Select(MapPosition);

      return new()
      {
        Data = [.. items]
      };
    }

    /// <summary>
    /// Convert remote position to local
    /// </summary>
    /// <param name="message"></param>
    public static Order MapPosition(PositionMessage message)
    {
      var volume = Math.Abs(message.Quantity ?? 0);
      var instrument = new Instrument
      {
        Name = message.Symbol
      };

      if (instrument.Derivative is not null)
      {
        instrument = instrument with
        {
          Leverage = 100,
          Type = InstrumentEnum.Options
        };
      }

      var action = new Operation
      {
        Instrument = instrument,
        Amount = volume
      };

      var order = new Order
      {
        Amount = volume,
        Operation = action,
        Type = OrderTypeEnum.Market,
        Price = Math.Abs((message.CostBasis / (instrument.Leverage ?? 1)) ?? 0),
        Side = message.Quantity > 0 ? OrderSideEnum.Long : OrderSideEnum.Short
      };

      return order;
    }
  }
}
