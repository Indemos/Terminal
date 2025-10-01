using Core.Enums;
using System;
using System.Collections.Generic;

namespace Core.Models
{
  public record OrderModel
  {
    /// <summary>
    /// Client order ID
    /// </summary>
    public string Id { get; init; } = $"{Guid.NewGuid()}";

    /// <summary>
    /// Group
    /// </summary>
    public string Descriptor { get; init; }

    /// <summary>
    /// Contract size
    /// </summary>
    public double? Amount { get; init; }

    /// <summary>
    /// Price the makes order active, e.g. limit price for stop limit order
    /// </summary>
    public double? ActivationPrice { get; init; }

    /// <summary>
    /// Desired price for the order to fill, e.g. stop price for stop order and limit price for limit order
    /// </summary>
    public double? Price { get; init; }

    /// <summary>
    /// Type
    /// </summary>
    public OrderTypeEnum? Type { get; init; } = OrderTypeEnum.Market;

    /// <summary>
    /// Side
    /// </summary>
    public OrderSideEnum? Side { get; init; }

    /// <summary>
    /// Time in force
    /// </summary>
    public OrderTimeSpanEnum? TimeSpan { get; init; } = OrderTimeSpanEnum.Gtc;

    /// <summary>
    /// Custom order type
    /// </summary>
    public InstructionEnum? Instruction { get; init; }

    /// <summary>
    /// Balance
    /// </summary>
    public BalanceModel Balance { get; init; } = new();

    /// <summary>
    /// Transaction
    /// </summary>
    public OperationModel Operation { get; init; }

    /// <summary>
    /// List of related orders in the hierarchy
    /// </summary>
    public IList<OrderModel> Orders { get; init; } = [];
  }
}
