using Core.Conventions;
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
    /// <param name="grainObserver"></param>
    Task<StatusResponse> Setup(Connection connection, ITradeObserver grainObserver);
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
    /// <param name="grainObserver"></param>
    public virtual async Task<StatusResponse> Setup(Connection connection, ITradeObserver grainObserver)
    {
      var cts = new CancellationTokenSource(connection.Timeout);

      state = connection;
      observer = grainObserver;
      connector = new()
      {
        Token = connection.AccessToken,
        SessionToken = connection.SessionToken,
      };

      return new()
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<OrdersResponse> Positions(PositionCriteria criteria)
    {
      var cts = new CancellationTokenSource(state.Timeout);
      var query = new Queries.Account.AccountRequest { AccountNumber = criteria.Account.Descriptor };
      var messages = await connector.GetPositions(query, cts.Token);
      var items = messages.Select(MapPosition);

      return new()
      {
        Data = [.. items]
      };
    }

    /// <summary>
    /// Map position
    /// </summary>
    /// <param name="message"></param>
    protected virtual Order MapPosition(PositionMessage message)
    {
      var volume = Math.Abs(message.Quantity ?? 0);
      var instrument = new Instrument
      {
        Name = message.Symbol
      };

      if (instrument.Name.Length > 10)
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
