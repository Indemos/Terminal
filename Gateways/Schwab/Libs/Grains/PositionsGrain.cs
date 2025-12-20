using Core.Enums;
using Core.Grains;
using Core.Models;
using Schwab.Messages;
using Schwab.Models;
using Schwab.Queries;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Schwab.Grains
{
  public interface ISchwabPositionsGrain : IPositionsGrain
  {
    /// <summary>
    /// Stamp
    /// </summary>
    /// <param name="accessToken"></param>
    Task<StatusResponse> Stamp(string accessToken);

    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    Task<StatusResponse> Setup(Connection connection);
  }

  public class SchwabPositionsGrain : PositionsGrain, ISchwabPositionsGrain
  {
    /// <summary>
    /// State
    /// </summary>
    protected Connection state;

    /// <summary>
    /// Connector
    /// </summary>
    protected SchwabBroker connector = new();

    /// <summary>
    /// Stamp
    /// </summary>
    /// <param name="accessToken"></param>
    public virtual async Task<StatusResponse> Stamp(string accessToken)
    {
      connector.AccessToken = accessToken;

      return new()
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    public virtual async Task<StatusResponse> Setup(Connection connection)
    {
      state = connection;
      connector.AccessToken = connection.AccessToken;

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
      var query = new AccountQuery { AccountCode = criteria.Account.Descriptor };
      var messages = await connector.GetPositions(query, cleaner.Token);
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
      var price = message.AveragePrice + message.Instrument.NetChange;
      var volume = message.LongQuantity + message.ShortQuantity;
      var point = new Price
      {
        Bid = price,
        Ask = price,
        Last = price
      };

      var instrumentType = MapInstrument(message.Instrument.AssetType);
      var instrument = new Instrument
      {
        Price = point,
        Name = message.Instrument.Symbol,
        Type = instrumentType
      };

      var action = new Operation
      {
        Amount = volume,
        Instrument = instrument,
        AveragePrice = message.AveragePrice
      };

      var order = new Order
      {
        Amount = volume,
        Operation = action,
        Type = OrderTypeEnum.Market,
        Side = MapSide(message),
        Descriptor = message.Instrument.Symbol,
        Price = message.AveragePrice
      };

      return order;
    }

    /// <summary>
    /// Map remote instrument
    /// </summary>
    /// <param name="assetType"></param>
    protected virtual InstrumentEnum? MapInstrument(string assetType)
    {
      switch (assetType?.ToUpper())
      {
        case "COE":
        case "ETF":
        case "EQUITY":
        case "MUTUAL_FUND": return InstrumentEnum.Shares;
        case "INDEX": return InstrumentEnum.Indices;
        case "BOND": return InstrumentEnum.Bonds;
        case "FOREX": return InstrumentEnum.Currencies;
        case "FUTURE": return InstrumentEnum.Futures;
        case "FUTURE_OPTION": return InstrumentEnum.FutureOptions;
        case "OPTION": return InstrumentEnum.Options;
      }

      return null;
    }

    /// <summary>
    /// Convert remote position side to local
    /// </summary>
    /// <param name="message"></param>
    protected virtual OrderSideEnum? MapSide(PositionMessage message)
    {
      switch (true)
      {
        case true when message.LongQuantity > 0: return OrderSideEnum.Long;
        case true when message.ShortQuantity > 0: return OrderSideEnum.Short;
      }

      return null;
    }
  }
}
