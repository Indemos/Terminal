namespace Terminal.Core.Enums
{
  public enum OrderStatusEnum : byte
  {
    None = 0,
    Placed = 1,
    Filled = 2,
    Closed = 3,
    Pending = 4,
    Expired = 5,
    Declined = 6,
    Canceled = 7,
    Completed = 8,
    Partitioned = 9
  }
}
