using System;
using System.Collections.Generic;
using Terminal.Core.Enums;

namespace Terminal.Core.Models
{
  public class OrderModel : ICloneable
  {
    /// <summary>
    /// Price the makes order active, e.g. limit price for stop limit order
    /// </summary>
    public virtual double? ActivationPrice { get; set; }

    /// <summary>
    /// Desired price for the order to fill, e.g. stop price for stop order and limit price for limit order
    /// </summary>
    public virtual double? Price { get; set; }

    /// <summary>
    /// Side
    /// </summary>
    public virtual OrderSideEnum? Side { get; set; }

    /// <summary>
    /// Type
    /// </summary>
    public virtual OrderTypeEnum? Type { get; set; }

    /// <summary>
    /// Time in force
    /// </summary>
    public virtual OrderTimeSpanEnum? TimeSpan { get; set; }

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
    public virtual Action<StateModel<OrderModel>> OrderStream { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public OrderModel()
    {
      Orders = [];
      OrderStream = o => { };
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
