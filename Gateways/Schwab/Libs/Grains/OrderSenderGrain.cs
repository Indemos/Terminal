using Core.Enums;
using Core.Models;
using Schwab.Messages;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Schwab.Grains
{
  public interface ISchwabOrderSenderGrain : ISchwabOrdersGrain
  {
  }

  public class SchwabOrderSenderGrain : SchwabOrdersGrain, ISchwabOrderSenderGrain
  {
    /// <summary>
    /// Send order
    /// </summary>
    /// <param name="order"></param>
    public override async Task<OrderGroupResponse> Send(Order order)
    {
      var message = MapOrder(order);
      var accountCode = order.Account.Name;
      var messageResponse = await broker.SendOrder(message, accountCode, CancellationToken.None);
      var response = new OrderResponse { Data = new() { Id = messageResponse.OrderId } };

      return new()
      {
        Data = [response]
      };
    }

    /// <summary>
    /// Convert remote order from brokerage to local record
    /// </summary>
    /// <param name="account"></param>
    /// <param name="order"></param>
    protected virtual OrderMessage MapOrder(Order order)
    {
      var action = order.Operation;
      var message = new OrderMessage
      {
        Session = "NORMAL",
        OrderStrategyType = "SINGLE",
        OrderType = MapOrderType(order.Type),
        Duration = MapTimeSpan(order.TimeSpan)
      };

      switch (order.Type)
      {
        case OrderTypeEnum.Stop: message.StopPrice = order.Price; break;
        case OrderTypeEnum.Limit: message.Price = order.Price; break;
        case OrderTypeEnum.StopLimit:
          message.Price = order.Price;
          message.StopPrice = order.ActivationPrice;
          break;
      }

      message.OrderLegCollection = [.. order
        .Orders
        .Where(o => o.Instruction is null)
        .Select(o => GetSubOrder(o, order))];

      message.ChildOrderStrategies = [.. order
        .Orders
        .Where(o => o.Instruction is InstructionEnum.Brace)
        .Where(o => Equals(o.Operation.Instrument.Name, order.Operation.Instrument.Name))
        .Select(o =>
        {
          var subOrder = MapOrder(o);
          subOrder.OrderLegCollection = [GetSubOrder(o, order)];
          return subOrder;
        })];

      if (order?.Amount is not 0)
      {
        message.OrderLegCollection.Add(GetSubOrder(order));
      }

      if (message.ChildOrderStrategies.Count is not 0)
      {
        message.OrderStrategyType = "TRIGGER";
      }

      return message;
    }

    /// <summary>
    /// Order type
    /// </summary>
    /// <param name="order"></param>
    protected virtual string MapOrderType(OrderTypeEnum? message)
    {
      switch (message)
      {
        case OrderTypeEnum.Stop: return "STOP";
        case OrderTypeEnum.Limit: return "LIMIT";
        case OrderTypeEnum.StopLimit: return "STOP_LIMIT";
      }

      return "MARKET";
    }

    /// <summary>
    /// Convert local time in force to remote
    /// </summary>
    /// <param name="span"></param>
    protected virtual string MapTimeSpan(OrderTimeSpanEnum? span)
    {
      switch (span)
      {
        case OrderTimeSpanEnum.DAY: return "DAY";
        case OrderTimeSpanEnum.FOK: return "FILL_OR_KILL";
      }

      return "GOOD_TILL_CANCEL";
    }

    /// <summary>
    /// Get external instrument type
    /// </summary>
    /// <param name="message"></param>
    protected virtual string MapInstrumentType(InstrumentEnum? message)
    {
      switch (message)
      {
        case InstrumentEnum.Shares: return "EQUITY";
        case InstrumentEnum.Options: return "OPTION";
      }

      return null;
    }

    /// <summary>
    /// Create leg in a combo-order
    /// </summary>
    /// <param name="order"></param>
    /// <param name="group"></param>
    /// <returns></returns>
    protected virtual OrderLegMessage GetSubOrder(Order order, Order group = null)
    {
      var action = order?.Operation ?? group?.Operation;
      var assetType = action?.Instrument?.Type ?? group?.Operation?.Instrument?.Type;
      var side = order?.Side ?? group?.Side;
      var instrument = new InstrumentMessage
      {
        AssetType = MapInstrumentType(assetType),
        Symbol = action.Instrument.Name
      };

      var response = new OrderLegMessage
      {
        Instrument = instrument,
        Quantity = order.Amount,
        Instruction = side is OrderSideEnum.Short ? "SELL" : "BUY"
      };

      return response;
    }
  }
}
