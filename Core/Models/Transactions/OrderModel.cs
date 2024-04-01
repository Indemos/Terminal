using System;
using System.Collections.Generic;
using Terminal.Core.Enums;

namespace Terminal.Core.Models
{
  public struct OrderModel
  {
    /// <summary>
    /// Price the makes order active, e.g. stop price for stop limit orders
    /// </summary>
    public double? ActivationPrice { get; set; }

    /// <summary>
    /// Side
    /// </summary>
    public OrderSideEnum? Side { get; set; }

    /// <summary>
    /// Type
    /// </summary>
    public OrderTypeEnum? Type { get; set; }

    /// <summary>
    /// Time in force
    /// </summary>
    public OrderTimeSpanEnum? TimeSpan { get; set; }

    /// <summary>
    /// Transaction
    /// </summary>
    public TransactionModel? Transaction { get; set; }

    /// <summary>
    /// List of related orders in the hierarchy
    /// </summary>
    public IList<OrderModel> Orders { get; set; }

    /// <summary>
    /// Order events
    /// </summary>
    public Action<StateModel<OrderModel>> OrderStream { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public OrderModel()
    {
      Orders = new List<OrderModel>();
      OrderStream = o => { };
    }
  }
}
