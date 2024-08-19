using System;
using System.Collections.Generic;
using System.Linq;
using Terminal.Core.Enums;

namespace Terminal.Core.Models
{
  public class PositionModel : ICloneable
  {
    /// <summary>
    /// Open price
    /// </summary>
    public virtual double? Price { get; set; }

    /// <summary>
    /// Actual PnL measured in account's currency
    /// </summary>
    public virtual double? GainLoss { get; set; }

    /// <summary>
    /// Min possible PnL in account's currency
    /// </summary>
    public virtual double? GainLossMin { get; set; }

    /// <summary>
    /// Max possible PnL in account's currency
    /// </summary>
    public virtual double? GainLossMax { get; set; }

    /// <summary>
    /// Actual PnL in points
    /// </summary>
    public virtual double? GainLossPoints { get; set; }

    /// <summary>
    /// Min possible PnL in points
    /// </summary>
    public virtual double? GainLossPointsMin { get; set; }

    /// <summary>
    /// Max possible PnL in points
    /// </summary>
    public virtual double? GainLossPointsMax { get; set; }

    /// <summary>
    /// Aggregated order
    /// </summary>
    public virtual OrderModel Order { get; set; }

    /// <summary>
    /// Estimated PnL in account's currency
    /// </summary>
    public virtual double? GainLossEstimate => GetGainLossEstimate();

    /// <summary>
    /// Estimated PnL in points
    /// </summary>
    public virtual double? GainLossPointsEstimate => GetGainLossPointsEstimate();

    /// <summary>
    /// Cummulative estimated PnL in account's currency for all positions in the same direction
    /// </summary>
    public virtual double? GainLossAverageEstimate => GetGainLossPointsEstimate();

    /// <summary>
    /// Cummulative estimated PnL in points for all positions in the same direction
    /// </summary>
    public virtual double? GainLossPointsAverageEstimate => GetGainLossEstimate();

    /// <summary>
    /// Estimate current or close price for one of the instruments in the order
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    public virtual double? GetSidePriceEstimate(OrderModel order)
    {
      var point = order.Transaction.Instrument.Point;

      if (point is not null)
      {
        switch (order.Side)
        {
          case OrderSideEnum.Buy: return point.Bid;
          case OrderSideEnum.Sell: return point.Ask;
        }
      }

      return null;
    }

    /// <summary>
    /// Position direction
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    protected double? GetSide(OrderModel order)
    {
      switch (order.Side)
      {
        case OrderSideEnum.Buy: return 1;
        case OrderSideEnum.Sell: return -1;
      }

      return null;
    }

    /// <summary>
    /// Estimated PnL in points
    /// </summary>
    /// <returns></returns>
    protected double? GetGainLossPointsEstimate()
    {
      var estimate = 0.0;

      if (Equals(Order.Instruction, InstructionEnum.Group))
      {
        estimate = Order
          .Orders
          .Where(o => Equals(o.Instruction, InstructionEnum.Side))
          .Sum(o => ((GetSidePriceEstimate(o) - o.Transaction.Price) * GetSide(o)) ?? 0);
      }
      else
      {
        estimate = ((GetSidePriceEstimate(Order) - Order.Transaction.Price) * GetSide(Order)) ?? 0;
      }

      GainLossPointsMin = Math.Min(GainLossPointsMin ?? estimate, estimate);
      GainLossPointsMax = Math.Max(GainLossPointsMax ?? estimate, estimate);

      return estimate;
    }

    /// <summary>
    /// Estimated PnL in account's currency
    /// </summary>
    /// <returns></returns>
    protected double? GetGainLossEstimate()
    {
      var action = Order.Transaction;
      var step = action.Instrument.StepValue / action.Instrument.StepSize;
      var estimate = action.Volume * (GetGainLossPointsEstimate() * step - action.Instrument.Commission) ?? 0.0;

      GainLossMin = Math.Min(GainLossMin ?? estimate, estimate);
      GainLossMax = Math.Max(GainLossMax ?? estimate, estimate);

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
