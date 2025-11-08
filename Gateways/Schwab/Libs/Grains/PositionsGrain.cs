using Core.Enums;
using Core.Grains;
using Core.Models;
using Schwab.Messages;
using Schwab.Models;
using Schwab.Queries;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Schwab.Grains
{
  public interface ISchwabPositionsGrain : IPositionsGrain
  {
    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    Task<StatusResponse> Setup(ConnectionModel connection);
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

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<IList<OrderModel>> Positions(CriteriaModel criteria)
    {
      var query = new AccountQuery { AccountCode = criteria.Account.Name };
      var messages = await broker.GetPositions(query, CancellationToken.None);

      return [.. messages.Select(MapPosition)];
    }

    /// <summary>
    /// Map position
    /// </summary>
    /// <param name="message"></param>
    protected virtual OrderModel MapPosition(PositionMessage message)
    {
      var price = message.AveragePrice + message.Instrument.NetChange;
      var volume = message.LongQuantity + message.ShortQuantity;
      var point = new PriceModel
      {
        Bid = price,
        Ask = price,
        Last = price
      };

      var instrumentType = MapInstrument(message.Instrument.AssetType);
      var instrument = new InstrumentModel
      {
        Price = point,
        Name = message.Instrument.Symbol,
        Type = instrumentType
      };

      var action = new OperationModel
      {
        Amount = volume,
        Instrument = instrument,
        AveragePrice = message.AveragePrice
      };

      var order = new OrderModel
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

      return InstrumentEnum.Group;
    }

    /// <summary>
    /// Convert remote position side to local
    /// </summary>
    /// <param name="message"></param>
    public static OrderSideEnum? MapSide(PositionMessage message)
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
