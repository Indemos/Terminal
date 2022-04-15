//using Alpaca.Markets;
//using Core.EnumSpace;

//namespace Gateway.Alpaca
//{
//  /// <summary>
//  /// Implementation
//  /// </summary>
//  public class MapInput
//  {
//    /// <summary>
//    /// Convert to external position type
//    /// </summary>
//    /// <param name="side"></param>
//    public static OrderSide? GetOrderSide(OrderSideEnum side)
//    {
//      switch (side)
//      {
//        case OrderSideEnum.Buy: return OrderSide.Buy;
//        case OrderSideEnum.Sell: return OrderSide.Sell;
//      }

//      return null;
//    }

//    /// <summary>
//    /// Convert to external order type
//    /// </summary>
//    /// <param name="orderType"></param>
//    public static OrderType? GetOrderType(OrderTypeEnum orderType)
//    {
//      switch (orderType)
//      {
//        case OrderTypeEnum.Stop: return OrderType.Stop;
//        case OrderTypeEnum.Limit: return OrderType.Limit;
//        case OrderTypeEnum.Market: return OrderType.Market;
//        case OrderTypeEnum.StopLimit: return OrderType.StopLimit;
//      }

//      return null;
//    }

//    /// <summary>
//    /// Convert time span 
//    /// </summary>
//    /// <param name="span"></param>
//    public static TimeInForce? GetTimeSpan(OrderTimeSpanEnum span)
//    {
//      switch (span)
//      {
//        case OrderTimeSpanEnum.Date: return TimeInForce.Day;
//        case OrderTimeSpanEnum.FillOrKill: return TimeInForce.Fok;
//        case OrderTimeSpanEnum.GoodTillCancel: return TimeInForce.Gtc;
//        case OrderTimeSpanEnum.ImmediateOrKill: return TimeInForce.Ioc;
//      }

//      return null;
//    }
//  }
//}
