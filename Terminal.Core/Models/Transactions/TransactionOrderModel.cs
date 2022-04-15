using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using Terminal.Core.EnumSpace;
using Terminal.Core.MessageSpace;

namespace Terminal.Core.ModelSpace
{
  /// <summary>
  /// Generic order model
  /// </summary>
  public interface ITransactionOrderModel : ITransactionModel
  {
    /// <summary>
    /// Side
    /// </summary>
    OrderSideEnum? Side { get; set; }

    /// <summary>
    /// Type
    /// </summary>
    OrderCategoryEnum? Category { get; set; }

    /// <summary>
    /// Time in force
    /// </summary>
    OrderTimeSpanEnum? TimeSpan { get; set; }

    /// <summary>
    /// Reference to the main order in the hierarchy
    /// </summary>
    ITransactionOrderModel Container { get; set; }

    /// <summary>
    /// List of related orders in the hierarchy
    /// </summary>
    IList<ITransactionOrderModel> Orders { get; set; }

    /// <summary>
    /// Order events
    /// </summary>
    ISubject<ITransactionMessage<ITransactionOrderModel>> OrderStream { get; set; }
  }

  /// <summary>
  /// Generic order model
  /// </summary>
  public class TransactionOrderModel : TransactionModel, ITransactionOrderModel
  {
    /// <summary>
    /// Side
    /// </summary>
    public virtual OrderSideEnum? Side { get; set; }

    /// <summary>
    /// Type
    /// </summary>
    public virtual OrderCategoryEnum? Category { get; set; }

    /// <summary>
    /// Time in force
    /// </summary>
    public virtual OrderTimeSpanEnum? TimeSpan { get; set; }

    /// <summary>
    /// Reference to the main order in the hierarchy
    /// </summary>
    public virtual ITransactionOrderModel Container { get; set; }

    /// <summary>
    /// List of related orders in the hierarchy
    /// </summary>
    public virtual IList<ITransactionOrderModel> Orders { get; set; }

    /// <summary>
    /// Order events
    /// </summary>
    public virtual ISubject<ITransactionMessage<ITransactionOrderModel>> OrderStream { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public TransactionOrderModel()
    {
      Id = Guid.NewGuid().ToString("N");
      Orders = new List<ITransactionOrderModel>();
      OrderStream = new Subject<ITransactionMessage<ITransactionOrderModel>>();
    }
  }
}
