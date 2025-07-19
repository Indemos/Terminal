using System;
using System.Collections.Generic;
using System.Linq;
using Terminal.Core.Domains;
using Terminal.Core.Enums;

namespace Terminal.Core.Models
{
  public class OrderModel : ICloneable
  {
    /// <summary>
    /// Name
    /// </summary>
    public virtual string Name => Transaction?.Instrument?.Name;

    /// <summary>
    /// Basis name
    /// </summary>
    public virtual string BasisName => Transaction?.Instrument?.Basis?.Name;

    /// <summary>
    /// Client order ID
    /// </summary>
    public virtual string Id { get; set; }

    /// <summary>
    /// Group
    /// </summary>
    public virtual string Descriptor { get; set; }

    /// <summary>
    /// Contract size
    /// </summary>
    public virtual double? Amount { get; set; }

    /// <summary>
    /// Current PnL
    /// </summary>
    public virtual double? Gain { get; set; }

    /// <summary>
    /// Min possible PnL in account's currency
    /// </summary>
    public virtual double? Min { get; set; }

    /// <summary>
    /// Max possible PnL in account's currency
    /// </summary>
    public virtual double? Max { get; set; }

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
      Id = $"{Guid.NewGuid()}";
      Descriptor = $"{Guid.NewGuid()}";
    }

    /// <summary>
    /// Position direction
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    public double? GetSide()
    {
      switch (Side)
      {
        case OrderSideEnum.Long: return 1;
        case OrderSideEnum.Short: return -1;
      }

      return null;
    }

    /// <summary>
    /// Position direction
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    public double? GetAmount()
    {
      var volume = Transaction?.Amount ?? 0;
      var sideVolume = Orders.Sum(o => o.Transaction?.Amount ?? 0);

      return volume + sideVolume;
    }

    /// <summary>
    /// Estimate open price for one of the instruments in the order
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    public virtual double? GetOpenPrice()
    {
      var point = Transaction.Instrument.Point;

      if (point is not null)
      {
        switch (Side)
        {
          case OrderSideEnum.Long: return point.Ask;
          case OrderSideEnum.Short: return point.Bid;
        }
      }

      return null;
    }

    /// <summary>
    /// Estimate close price for one of the instruments in the order
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    public virtual double? GetClosePrice()
    {
      var point = Transaction.Instrument.Point;

      if (point is not null)
      {
        switch (Side)
        {
          case OrderSideEnum.Long: return point.Bid;
          case OrderSideEnum.Short: return point.Ask;
        }
      }

      return null;
    }

    /// <summary>
    /// Estimated PnL in points for one side of the order
    /// </summary>
    public double? GetPointsEstimate()
    {
      return ((Transaction.Price ?? GetClosePrice()) - Transaction.AveragePrice) * GetSide();
    }

    /// <summary>
    /// Estimated PnL in account's currency for one side of the order
    /// </summary>
    public double? GetEstimate()
    {
      var amount = Transaction.Amount;
      var instrument = Transaction.Instrument;
      var step = instrument.StepValue / instrument.StepSize;
      var estimate = amount * GetPointsEstimate() * step * instrument.Leverage - instrument.Commission;

      Gain = estimate ?? Gain ?? 0;
      Min = Math.Min(Min ?? 0, Gain.Value);
      Max = Math.Max(Max ?? 0, Gain.Value);

      return estimate;
    }

    /// <summary>
    /// Clone
    /// </summary>
    public virtual object Clone()
    {
      var copy = MemberwiseClone() as OrderModel;

      copy.Transaction = Transaction?.Clone() as TransactionModel;

      return copy;
    }
  }
}
