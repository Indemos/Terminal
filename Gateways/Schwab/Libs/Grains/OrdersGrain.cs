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
  public interface ISchwabOrdersGrain : IOrdersGrain
  {
    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    Task<StatusResponse> Setup(Connection connection);
  }

  public class SchwabOrdersGrain : OrdersGrain, ISchwabOrdersGrain
  {
    /// <summary>
    /// State
    /// </summary>
    protected Connection state;

    /// <summary>
    /// Connector
    /// </summary>
    protected SchwabBroker connector;

    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    public virtual async Task<StatusResponse> Setup(Connection connection)
    {
      state = connection;
      connector = new()
      {
        ClientId = connection.Id,
        ClientSecret = connection.Secret,
        AccessToken = connection.AccessToken,
        RefreshToken = connection.RefreshToken
      };

      await connector.Connect();

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
      var query = new OrderQuery { AccountCode = criteria.Account.Descriptor};
      var messages = await connector.GetOrders(query, cleaner.Token);
      var items = messages.Select(MapOrder);

      return new()
      {
        Data = [.. items]
      };
    }

    /// <summary>
    /// Message to order
    /// </summary>
    /// <param name="message"></param>
    protected virtual Order MapOrder(OrderMessage message)
    {
      var orders = message?.OrderLegCollection ?? [];
      var (stopPrice, activationPrice, orderType) = MapPrice(message);
      var name = orders
        .Select(o => o.Instrument.UnderlyingSymbol ?? o.Instrument.Symbol)
        .Distinct();

      var instrument = new Instrument
      {
        Name = string.Join(" / ", name)
      };

      var action = new Operation
      {
        Id = message.OrderId,
        Amount = message.FilledQuantity,
        Time = message.EnteredTime?.Ticks,
        Status = OrderStatusEnum.Order
      };

      var order = new Order
      {
        Type = orderType,
        Price = stopPrice,
        Operation = action,
        ActivationPrice = activationPrice,
        TimeSpan = MapTimeSpan(message),
        Amount = message.Quantity
      };

      if (orders.Count is not 0)
      {
        foreach (var subOrder in orders)
        {
          var subInstrument = new Instrument
          {
            Name = subOrder.Instrument.Symbol,
            Type = MapInstrument(subOrder.Instrument.AssetType)
          };

          var subAction = new Operation
          {
            Instrument = subInstrument,
            Amount = subOrder.Quantity
          };

          order.Orders.Add(new Order
          {
            Operation = subAction,
            Amount = subOrder.Quantity,
            Side = MapSide(subOrder.Instruction)
          });
        }
      }

      return order;
    }

    /// <summary>
    /// Map remote order
    /// </summary>
    /// <param name="status"></param>
    public static OrderSideEnum? MapSide(string status)
    {
      switch (status?.ToUpper())
      {
        case "BUY":
        case "DEBIT":
        case "BUY_TO_OPEN":
        case "BUY_TO_CLOSE":
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
    /// Map remote price
    /// </summary>
    /// <param name="message"></param>
    protected virtual (double? price, double? activationPrice, OrderTypeEnum? orderType) MapPrice(OrderMessage message)
    {
      switch (message.OrderType)
      {
        case "LIMIT": return (message.Price, null, OrderTypeEnum.Limit);
        case "STOP": return (message.StopPrice, null, OrderTypeEnum.Stop);
        case "STOP_LIMIT": return (message.Price, message.StopPrice, OrderTypeEnum.StopLimit);
      };

      return (null, null, OrderTypeEnum.Market);
    }

    /// <summary>
    /// Map remote duration
    /// </summary>
    /// <param name="message"></param>
    protected virtual OrderTimeSpanEnum? MapTimeSpan(OrderMessage message)
    {
      switch (message?.Duration?.ToUpper())
      {
        case "DAY": return OrderTimeSpanEnum.DAY;
        case "FILL_OR_KILL": return OrderTimeSpanEnum.FOK;
        case "GOOD_TILL_CANCEL": return OrderTimeSpanEnum.GTC;
        case "IMMEDIATE_OR_CANCEL": return OrderTimeSpanEnum.IOC;
      }

      return null;
    }
  }
}
