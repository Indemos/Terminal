namespace Terminal.Core.EnumSpace
{
  public enum OrderStatusEnum : byte
  {
    None = 0,
    Placed = 1,
    Filled = 2,
    Closed = 3,
    Expired = 4,
    Declined = 5,
    Cancelled = 6,
    Completed = 7,
    PartiallyFilled = 8
  }
}
