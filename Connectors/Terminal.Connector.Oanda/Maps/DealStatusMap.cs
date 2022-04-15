using Core.EnumSpace;

namespace Gateway.Oanda.ModelSpace
{
  public class DealStatusMap
  {
    public static OrderStatusEnum? Input(string orderStatus)
    {
      switch (orderStatus)
      {
        case "OPEN": return OrderStatusEnum.Filled;
        case "CLOSED": return OrderStatusEnum.Closed;
      }

      return null;
    }
  }
}
