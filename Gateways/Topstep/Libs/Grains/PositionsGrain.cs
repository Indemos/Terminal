using Core.Enums;
using Core.Grains;
using Core.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Topstep.Models;
using TopstepX;
using TopstepX.Models.Orders;
using TopstepX.Models.Positions;

namespace Topstep.Grains
{
  public interface ITopstepPositionsGrain : IPositionsGrain
  {
    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    Task<StatusResponse> Setup(Connection connection);

    /// <summary>
    /// Validate session token
    /// </summary>
    /// <param name="session"></param>
    Task<StatusResponse> Validate(string session);
  }

  public class TopstepPositionsGrain : PositionsGrain, ITopstepPositionsGrain
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
    public virtual async Task<StatusResponse> Setup(Connection connection)
    {
      var response = new StatusResponse() { Data = StatusEnum.Active };

      state = connection;
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

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<OrdersResponse> Positions(PositionCriteria criteria)
    {
      var cts = new CancellationTokenSource(state.Timeout);
      var query = new SearchPositionRequest { accountId = int.Parse(criteria.Account.Descriptor) };
      var messages = await connector.PositionSearchOpen(query);
      var items = messages.positions.Select(MapPosition);

      return new()
      {
        Data = [.. items]
      };
    }

    /// <summary>
    /// Map position
    /// </summary>
    /// <param name="message"></param>
    protected virtual Order MapPosition(PositionModel message)
    {
      var volume = Math.Abs(message.size);
      var instrument = new Instrument
      {
        Id = message.contractId,
        Name = message.contractDisplayName,
        Type = InstrumentEnum.Futures
      };

      var operation = new Operation
      {
        Id = $"{message.id}",
        Amount = volume,
        Instrument = instrument,
        AveragePrice = message.averagePrice,
        Time = message.creationTimestamp.Ticks
      };

      var order = new Order
      {
        Amount = volume,
        Operation = operation,
        Type = OrderTypeEnum.Market,
        Price = message.averagePrice,
        Time = message.creationTimestamp.Ticks,
        Side = MapSide(message)
      };

      return order;
    }

    /// <summary>
    /// Map side
    /// </summary>
    /// <param name="status"></param>
    protected virtual OrderSideEnum? MapSide(PositionModel message)
    {
      switch (message.type)
      {
        case PositionType.Long: return OrderSideEnum.Long;
        case PositionType.Short: return OrderSideEnum.Short;
      }

      return null;
    }
  }
}
