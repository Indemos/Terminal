using Core.Common.Enums;

namespace Core.Common.States
{
  public record OperationState
  {
    /// <summary>
    /// Id
    /// </summary>
    public string Id { get; init; }

    /// <summary>
    /// Size of partially filled contract
    /// </summary>
    public double? Amount { get; init; }

    /// <summary>
    /// Open price 
    /// </summary>
    public double? AveragePrice { get; init; }

    /// <summary>
    /// Close price 
    /// </summary>
    public double? Price { get; init; }

    /// <summary>
    /// Time stamp
    /// </summary>
    public long? Time { get; init; }

    /// <summary>
    /// Status of the order, e.g. Pending
    /// </summary>
    public OrderStatusEnum? Status { get; init; }

    /// <summary>
    /// Instrument to buy or sell
    /// </summary>
    public InstrumentState Instrument { get; init; }
  }
}
