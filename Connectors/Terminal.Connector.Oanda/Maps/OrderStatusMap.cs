using Core.EnumSpace;

namespace Gateway.Oanda.ModelSpace
{
  public class OrderStatusMap
  {
    public static OrderStatusEnum? Input(string orderStatus)
    {
      switch (orderStatus)
      {
        case "FILLED": return OrderStatusEnum.Filled;
        case "PENDING": return OrderStatusEnum.Placed;
        case "CANCELLED": return OrderStatusEnum.Cancelled;
        case "TRIGGERED": return OrderStatusEnum.Completed;
      }

      return null;
    }
  }
}
