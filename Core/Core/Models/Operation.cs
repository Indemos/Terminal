using Core.Enums;

namespace Core.Models
{
  public record Operation
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
    public Instrument Instrument { get; init; }
  }
}
