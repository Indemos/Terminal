using System;
using System.Collections.Generic;
using Terminal.Core.Enums;
using Terminal.Core.Extensions;

namespace Terminal.Core.Models
{
  public class OrderModel : ICloneable
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
    /// Price the makes order active, e.g. limit price for stop limit order
    /// </summary>
    public virtual double? ActivationPrice { get; set; }

    /// <summary>
    /// Desired price for the order to fill, e.g. stop price for stop order and limit price for limit order
    /// </summary>
    public virtual double? Price { get; set; }

    /// <summary>
    /// Type
    /// </summary>
    public virtual OrderTypeEnum? Type { get; set; }

    /// <summary>
    /// Side
    /// </summary>
    public virtual OrderSideEnum? Side { get; set; }

    /// <summary>
    /// Time in force
    /// </summary>
    public virtual OrderTimeSpanEnum? TimeSpan { get; set; }

    /// <summary>
    /// Custom order type
    /// </summary>
    public virtual InstructionEnum? Instruction { get; set; }

    /// <summary>
    /// Transaction
    /// </summary>
    public virtual TransactionModel Transaction { get; set; }

    /// <summary>
    /// List of related orders in the hierarchy
    /// </summary>
    public virtual IList<OrderModel> Orders { get; set; }

    /// <summary>
    /// Order events
    /// </summary>
    public virtual Action<MessageModel<OrderModel>> OrderStream { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public OrderModel()
    {
      Orders = [];
      OrderStream = o => { };
    }

    /// <summary>
    /// Position direction
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    public double? GetDirection()
    {
      switch (Side)
      {
        case OrderSideEnum.Buy: return 1;
        case OrderSideEnum.Sell: return -1;
      }

      return null;
    }

    /// <summary>
    /// Estimate current or close price for one of the instruments in the order
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    public virtual double? GetPriceEstimate()
    {
      var point = Transaction.Instrument.Point;

      if (point is not null)
      {
        switch (Side)
        {
          case OrderSideEnum.Buy: return point.Bid;
          case OrderSideEnum.Sell: return point.Ask;
        }
      }

      return null;
    }

    /// <summary>
    /// Estimated PnL in points for one side of the order
    /// </summary>
    /// <param name="price"></param>
    /// <returns></returns>
    public double? GetPointsEstimate(double? price = null)
    {
      if (Transaction is not null)
      {
        return (((price ?? GetPriceEstimate()) - Price) * GetDirection()) ?? 0;
      }

      return 0;
    }

    /// <summary>
    /// Estimated PnL in account's currency for one side of the order
    /// </summary>
    /// <param name="price"></param>
    /// <returns></returns>
    public double? GetGainEstimate(double? price = null)
    {
      var estimate = 0.0;
      var instrument = Transaction.Instrument;
      var step = instrument.StepValue / instrument.StepSize;
      var volume = Transaction.CurrentVolume;

      if (volume.Is(0))
      {
        volume = Transaction.Volume;
      }

      if (Transaction is not null)
      {
        estimate = (volume * GetPointsEstimate(price) * step - instrument.Commission) ?? 0;
      }

      GainMin = Math.Min(GainMin ?? estimate, estimate);
      GainMax = Math.Max(GainMax ?? estimate, estimate);

      return estimate;
    }

    /// <summary>
    /// Clone
    /// </summary>
    public virtual object Clone()
    {
      var clone = MemberwiseClone() as OrderModel;

      clone.Transaction = Transaction?.Clone() as TransactionModel;

      return clone;
    }
  }
}
