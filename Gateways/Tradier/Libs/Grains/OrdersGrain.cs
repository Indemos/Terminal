using Core.Enums;
using Core.Grains;
using Core.Models;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tradier.Messages.Account;
using Tradier.Models;
using Tradier.Queries.Account;

namespace Tradier.Grains
{
  public interface ITradierOrdersGrain : IOrdersGrain
  {
    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    Task<StatusResponse> Setup(Connection connection);
  }

  public class TradierOrdersGrain : OrdersGrain, ITradierOrdersGrain
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
    public override async Task<OrdersResponse> Orders(Criteria criteria)
    {
      var cleaner = new CancellationTokenSource(state.Timeout);
      var query = new AccountRequest { AccountNumber = criteria.Account.Descriptor };
      var messages = await connector.GetOrders(query);
      var items = messages.SelectMany(message =>
      {
        var orders = message.Orders.Select(o => MapOrder(o));

        if (message.Quantity is not null && message.Symbol is not null)
        {
          orders = orders.Append(MapOrder(message));
        }

        return orders;
      });


      return new()
      {
        Data = [.. items]
      };
    }

    /// <summary>
    /// Map order
    /// </summary>
    /// <param name="message"></param>
    protected virtual Order MapOrder(OrderMessage message)
    {
      var basis = new Instrument
      {
        Name = message.Symbol,
        Type = GetInstrumentType(message.Class)
      };

      var instrument = new Instrument
      {
        Basis = basis,
        Type = GetInstrumentType(message.Class),
        Name = message.OptionSymbol ?? message.Symbol
      };

      var action = new Operation
      {
        Id = $"{message.Id}",
        Amount = message.Quantity,
        Time = message.TransactionDate?.Ticks,
        Status = OrderStatusEnum.Order,
        Instrument = instrument
      };

      var order = new Order
      {
        Id = $"{message.Id}",
        Operation = action,
        Type = OrderTypeEnum.Market,
        Amount = message.Quantity,
        Side = MapSide(message)
      };

      switch (message?.Type?.ToUpper())
      {
        case "DEBIT":
        case "CREDIT":
        case "LIMIT": order = order with { Type = OrderTypeEnum.Limit, Price = message.Price }; break;
        case "STOP": order = order with { Type = OrderTypeEnum.Stop, Price = message.StopPrice }; break;
        case "STOP_LIMIT":

          order = order with
          {
            Price = message.Price,
            Type = OrderTypeEnum.StopLimit,
            ActivationPrice = message.StopPrice
          };

          break;
      }

      return order;
    }

    /// <summary>
    /// Map side
    /// </summary>
    /// <param name="status"></param>
    protected virtual OrderSideEnum? MapSide(OrderMessage message)
    {
      switch (message.Side?.ToUpper())
      {
        case "BUY":
        case "DEBIT":
        case "BUY_TO_OPEN":
        case "BUY_TO_CLOSE":
        case "BUY_TO_COVER":
        case "NET_DEBIT": return OrderSideEnum.Long;

        case "SELL":
        case "CREDIT":
        case "SELL_SHORT":
        case "SELL_TO_OPEN":
        case "SELL_TO_CLOSE":
        case "NET_CREDIT": return OrderSideEnum.Short;
      }

      return null;
    }

    /// <summary>
    /// Map asset type
    /// </summary>
    /// <param name="assetType"></param>
    protected virtual InstrumentEnum? GetInstrumentType(string assetType)
    {
      switch (assetType?.ToUpper())
      {
        case "EQUITY": return InstrumentEnum.Shares;
        case "INDEX": return InstrumentEnum.Indices;
        case "FUTURE": return InstrumentEnum.Futures;
        case "OPTION": return InstrumentEnum.Options;
      }

      return null;
    }
  }
}
