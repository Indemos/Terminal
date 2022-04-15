using Core.EnumSpace;

namespace Gateway.Oanda.ModelSpace
{
  public class OrderTimeSpanMap
  {
    public static OrderTimeSpanEnum? Input(string orderTimeSpan)
    {
      switch (orderTimeSpan)
      {
        case "GTD":
        case "GFD": return OrderTimeSpanEnum.Date;
        case "GTC": return OrderTimeSpanEnum.GTC;
        case "FOK": return OrderTimeSpanEnum.FOK;
        case "IOC": return OrderTimeSpanEnum.IOC;
      }

      return null;
    }
  }
}
