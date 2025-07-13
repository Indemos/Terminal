using System;
using Terminal.Core.Domains;
using Terminal.Core.Enums;

namespace Terminal.Core.Models
{
  public class TransactionModel : ICloneable
  {
    /// <summary>
    /// Id
    /// </summary>
    public virtual string Id { get; set; }

    /// <summary>
    /// Size of partially filled contract
    /// </summary>
    public virtual double? Amount { get; set; }

    /// <summary>
    /// Open price 
    /// </summary>
    public virtual double? AveragePrice { get; set; }

    /// <summary>
    /// Close price 
    /// </summary>
    public virtual double? Price { get; set; }

    /// <summary>
    /// Time stamp
    /// </summary>
    public virtual DateTime? Time { get; set; }

    /// <summary>
    /// Status of the order, e.g. Pending
    /// </summary>
    public virtual OrderStatusEnum? Status { get; set; }

    /// <summary>
    /// Instrument to buy or sell
    /// </summary>
    public virtual InstrumentModel Instrument { get; set; }

    /// <summary>
    /// Clone
    /// </summary>
    public virtual object Clone() => MemberwiseClone() as TransactionModel;
  }
}
