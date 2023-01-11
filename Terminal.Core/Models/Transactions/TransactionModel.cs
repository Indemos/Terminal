using System;
using Terminal.Core.EnumSpace;

namespace Terminal.Core.ModelSpace
{
  /// <summary>
  /// Generic order model
  /// </summary>
  public interface ITransactionModel : IBaseModel
  {
    /// <summary>
    /// Contract size
    /// </summary>
    double? Volume { get; set; }

    /// <summary>
    /// Open price for the order
    /// </summary>
    double? Price { get; set; }

    /// <summary>
    /// Price the makes order active, e.g. stop price for stop limit orders
    /// </summary>
    double? ActivationPrice { get; set; }

    /// <summary>
    /// Time stamp
    /// </summary>
    DateTime? Time { get; set; }

    /// <summary>
    /// Transaction type, e.g. withdrawal or order placement
    /// </summary>
    OperationEnum? Operation { get; set; }

    /// <summary>
    /// Status of the order, e.g. Pending
    /// </summary>
    OrderStatusEnum? Status { get; set; }

    /// <summary>
    /// Instrument to buy or sell
    /// </summary>
    IInstrumentModel Instrument { get; set; }
  }

  /// <summary>
  /// Generic order model
  /// </summary>
  public class TransactionModel : BaseModel, ITransactionModel
  {
    /// <summary>
    /// Contract size
    /// </summary>
    public virtual double? Volume { get; set; }

    /// <summary>
    /// Open price for the order
    /// </summary>
    public virtual double? Price { get; set; }

    /// <summary>
    /// Price the makes order active, e.g. stop price for stop limit orders
    /// </summary>
    public virtual double? ActivationPrice { get; set; }

    /// <summary>
    /// Time stamp
    /// </summary>
    public virtual DateTime? Time { get; set; }

    /// <summary>
    /// Transaction type, e.g. withdrawal or order placement
    /// </summary>
    public virtual OperationEnum? Operation { get; set; }

    /// <summary>
    /// Status of the order, e.g. Pending
    /// </summary>
    public virtual OrderStatusEnum? Status { get; set; }

    /// <summary>
    /// Instrument to buy or sell
    /// </summary>
    public virtual IInstrumentModel Instrument { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public TransactionModel()
    {
      Time = DateTime.Now;
      Instrument = new InstrumentModel();
    }
  }
}
