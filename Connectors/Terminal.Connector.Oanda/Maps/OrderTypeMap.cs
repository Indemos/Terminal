using Core.EnumSpace;

namespace Gateway.Oanda.ModelSpace
{
  public class OrderTypeMap
  {
    public static OrderTypeEnum? Input(string orderType)
    {
      switch (orderType)
      {
        case "MARKET": 
        case "MARKET_IF_TOUCHED": 
        case "FIXED_PRICE": return OrderTypeEnum.Market;

        case "TAKE_PROFIT":
        case "LIMIT": return OrderTypeEnum.Limit;

        case "STOP_LOSS":
        case "TRAILING_STOP_LOSS":
        case "GUARANTEED_STOP_LOSS":
        case "STOP": return OrderTypeEnum.Stop;
      }

      return null;
    }
  }
}
