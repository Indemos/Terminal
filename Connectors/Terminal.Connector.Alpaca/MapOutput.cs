//using Core.EnumSpace;

//namespace Gateway.Alpaca
//{
//  /// <summary>
//  /// Implementation
//  /// </summary>
//  public class MapOutput
//  {
//    /// <summary>
//    /// Convert position direction
//    /// </summary>
//    /// <param name="positionSide"></param>
//    public static double GetDirection(OrderSideEnum positionSide)
//    {
//      switch (positionSide)
//      {
//        case OrderSideEnum.Buy: return 1.0;
//        case OrderSideEnum.Sell: return -1.0;
//      }

//      return 0.0;
//    }

//    /// <summary>
//    /// Convert position side
//    /// </summary>
//    /// <param name="side"></param>
//    public static OrderSideEnum? GetPositionSide(PositionSide side)
//    {
//      switch (side)
//      {
//        case PositionSide.Long: return OrderSideEnum.Buy;
//        case PositionSide.Short: return OrderSideEnum.Sell;
//      }

//      return null;
//    }

//    /// <summary>
//    /// Convert position type
//    /// </summary>
//    /// <param name="side"></param>
//    public static OrderSideEnum? GetOrderSide(OrderSide side)
//    {
//      switch (side)
//      {
//        case OrderSide.Buy: return OrderSideEnum.Buy;
//        case OrderSide.Sell: return OrderSideEnum.Sell;
//      }

//      return null;
//    }

//    /// <summary>
//    /// Convert time span 
//    /// </summary>
//    /// <param name="span"></param>
//    public static OrderTimeSpanEnum? GetTimeSpan(TimeInForce span)
//    {
//      switch (span)
//      {
//        case TimeInForce.Day: return OrderTimeSpanEnum.Date;
//        case TimeInForce.Fok: return OrderTimeSpanEnum.FillOrKill;
//        case TimeInForce.Gtc: return OrderTimeSpanEnum.GoodTillCancel;
//        case TimeInForce.Ioc: return OrderTimeSpanEnum.ImmediateOrKill;
//      }

//      return null;
//    }

//    /// <summary>
//    /// Convert order type
//    /// </summary>
//    /// <param name="orderType"></param>
//    public static OrderTypeEnum? GetOrderType(OrderType orderType)
//    {
//      switch (orderType)
//      {
//        case OrderType.Stop: return OrderTypeEnum.Stop;
//        case OrderType.Limit: return OrderTypeEnum.Limit;
//        case OrderType.Market: return OrderTypeEnum.Market;
//        case OrderType.StopLimit: return OrderTypeEnum.StopLimit;
//      }

//      return null;
//    }

//    /// <summary>
//    /// Convert order status
//    /// </summary>
//    /// <param name="status"></param>
//    public static OrderStatusEnum? GetOrderStatus(OrderStatus status)
//    {
//      switch (status)
//      {
//        case OrderStatus.New:
//        case OrderStatus.Held:
//        case OrderStatus.Accepted:
//        case OrderStatus.Replaced:
//        case OrderStatus.Suspended:
//        case OrderStatus.Calculated:
//        case OrderStatus.PendingNew:
//        case OrderStatus.DoneForDay:
//        case OrderStatus.PendingReplace:
//        case OrderStatus.AcceptedForBidding:

//          return OrderStatusEnum.Placed;

//        case OrderStatus.Canceled:
//        case OrderStatus.PendingCancel:

//          return OrderStatusEnum.Cancelled;

//        case OrderStatus.Expired:

//          return OrderStatusEnum.Expired;

//        case OrderStatus.Fill:
//        case OrderStatus.Filled:

//          return OrderStatusEnum.Filled;

//        case OrderStatus.PartialFill:
//        case OrderStatus.PartiallyFilled:

//          return OrderStatusEnum.PartiallyFilled;

//        case OrderStatus.Stopped:
//        case OrderStatus.Rejected:

//          return OrderStatusEnum.Declined;
//      }

//      return null;
//    }
//  }
//}
