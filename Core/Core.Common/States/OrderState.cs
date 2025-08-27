using Core.Common.Enums;
using Orleans;
using System.Collections.Generic;

namespace Core.Common.States
{
  [Immutable]
  [GenerateSerializer]
  public record OrderState
  {
    /// <summary>
    /// Client order ID
    /// </summary>
    [Id(0)] public string Id { get; init; }

    /// <summary>
    /// Group
    /// </summary>
    [Id(1)] public string Descriptor { get; init; }

    /// <summary>
    /// Contract size
    /// </summary>
    [Id(2)] public double? Amount { get; init; }

    /// <summary>
    /// Current PnL
    /// </summary>
    [Id(3)] public double? Gain { get; init; }

    /// <summary>
    /// Min possible PnL in account's currency
    /// </summary>
    [Id(4)] public double? Min { get; init; }

    /// <summary>
    /// Max possible PnL in account's currency
    /// </summary>
    [Id(5)] public double? Max { get; init; }

    /// <summary>
    /// Price the makes order active, e.g. limit price for stop limit order
    /// </summary>
    [Id(6)] public double? ActivationPrice { get; init; }

    /// <summary>
    /// Desired price for the order to fill, e.g. stop price for stop order and limit price for limit order
    /// </summary>
    [Id(7)] public double? Price { get; init; }

    /// <summary>
    /// Type
    /// </summary>
    [Id(8)] public OrderTypeEnum? Type { get; init; } = OrderTypeEnum.Market;

    /// <summary>
    /// Side
    /// </summary>
    [Id(9)] public OrderSideEnum? Side { get; init; }

    /// <summary>
    /// Time in force
    /// </summary>
    [Id(10)] public OrderTimeSpanEnum? TimeSpan { get; init; } = OrderTimeSpanEnum.Gtc;

    /// <summary>
    /// Custom order type
    /// </summary>
    [Id(11)] public InstructionEnum? Instruction { get; init; }

    /// <summary>
    /// Transaction
    /// </summary>
    [Id(12)] public OperationState Operation { get; init; }

    /// <summary>
    /// List of related orders in the hierarchy
    /// </summary>
    [Id(13)] public List<OrderState> Orders { get; init; } = new();
  }
}
