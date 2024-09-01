using System;
using System.Linq;
using Terminal.Core.Enums;

namespace Terminal.Core.Models
{
  public class PositionModel : ICloneable
  {
    /// <summary>
    /// Min possible PnL in account's currency
    /// </summary>
    public virtual double? GainMin { get; set; }

    /// <summary>
    /// Max possible PnL in account's currency
    /// </summary>
    public virtual double? GainMax { get; set; }

    /// <summary>
    /// Aggregated order
    /// </summary>
    public virtual OrderModel Order { get; set; }

    /// <summary>
    /// Estimated PnL in account's currency for the whole order
    /// </summary>
    /// <returns></returns>
    public double? GetGainEstimate()
    {
      var estimate = 0.0;

      if (Order.Transaction is not null)
      {
        estimate += Order.GetGainEstimate() ?? 0.0;
      }

      estimate += Order
        .Orders
        .Where(o => o.Instruction is InstructionEnum.Side)
        .Sum(o => o.GetGainEstimate()) ?? 0.0;

      GainMin = Math.Min(GainMin ?? estimate, estimate);
      GainMax = Math.Max(GainMax ?? estimate, estimate);

      return estimate;
    }

    /// <summary>
    /// Clone
    /// </summary>
    public virtual object Clone()
    {
      var clone = MemberwiseClone() as PositionModel;

      clone.Order = Order?.Clone() as OrderModel;

      return clone;
    }
  }
}
