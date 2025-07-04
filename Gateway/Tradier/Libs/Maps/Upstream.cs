using Terminal.Core.Enums;
using Terminal.Core.Models;

namespace Tradier.Mappers
{
  public class Upstream
  {
    /// <summary>
    /// Get external instrument type
    /// </summary>
    /// <param name="position"></param>
    /// <returns></returns>
    public static string GetSide(OrderModel position)
    {
      var option = position.Instrument.Type is InstrumentEnum.Options or InstrumentEnum.FutureOptions;

      if (option)
      {
        switch (true)
        {
          case true when position is null && position.Side is OrderSideEnum.Long:
          case true when position is not null && position.Side is OrderSideEnum.Long && position.Side is OrderSideEnum.Long: return "buy_to_open";
          case true when position is not null && position.Side is OrderSideEnum.Short && position.Side is OrderSideEnum.Long: return "buy_to_close";
          case true when position is null && position.Side is OrderSideEnum.Short:
          case true when position is not null && position.Side is OrderSideEnum.Short && position.Side is OrderSideEnum.Short: return "sell_to_open";
          case true when position is not null && position.Side is OrderSideEnum.Long && position.Side is OrderSideEnum.Short: return "sell_to_close";
        }
      }

      switch (true)
      {
        case true when position is null && position.Side is OrderSideEnum.Long: 
        case true when position is not null && position.Side is OrderSideEnum.Long && position.Side is OrderSideEnum.Long: return "buy";
        case true when position is not null && position.Side is OrderSideEnum.Short && position.Side is OrderSideEnum.Long: return "buy_to_cover";
        case true when position is null && position.Side is OrderSideEnum.Short: 
        case true when position is not null && position.Side is OrderSideEnum.Short && position.Side is OrderSideEnum.Short: return "sell_short";
        case true when position is not null && position.Side is OrderSideEnum.Long && position.Side is OrderSideEnum.Short: return "sell";
      }

      return null;
    }

    /// <summary>
    /// Order side
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    public static string GetOrderType(OrderTypeEnum? message)
    {
      switch (message)
      {
        case OrderTypeEnum.Stop: return "stop";
        case OrderTypeEnum.Limit: return "limit";
        case OrderTypeEnum.StopLimit: return "stop_limit";
      }

      return "market";
    }

    /// <summary>
    /// Convert local time in force to remote
    /// </summary>
    /// <param name="span"></param>
    /// <returns></returns>
    public static string GetTimeSpan(OrderTimeSpanEnum? span)
    {
      switch (span)
      {
        case OrderTimeSpanEnum.Am: return "pre";
        case OrderTimeSpanEnum.Pm: return "post";
        case OrderTimeSpanEnum.Day: return "day";
      }

      return "gtc";
    }
  }
}
