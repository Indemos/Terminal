using Core.Common.Enums;
using Orleans;
using System;

namespace Core.Common.States
{
  [Immutable]
  [GenerateSerializer]
  public record OperationState
  {
    /// <summary>
    /// Id
    /// </summary>
    [Id(0)] public string Id { get; init; }

    /// <summary>
    /// Size of partially filled contract
    /// </summary>
    [Id(1)] public double? Amount { get; init; }

    /// <summary>
    /// Open price 
    /// </summary>
    [Id(2)] public double? AveragePrice { get; init; }

    /// <summary>
    /// Close price 
    /// </summary>
    [Id(3)] public double? Price { get; init; }

    /// <summary>
    /// Time stamp
    /// </summary>
    [Id(4)] public DateTime? Time { get; init; }

    /// <summary>
    /// Status of the order, e.g. Pending
    /// </summary>
    [Id(5)] public OrderStatusEnum? Status { get; init; }

    /// <summary>
    /// Instrument to buy or sell
    /// </summary>
    [Id(6)] public InstrumentState Instrument { get; init; }
  }
}
