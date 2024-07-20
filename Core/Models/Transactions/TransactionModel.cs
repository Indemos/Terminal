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
    public string Id { get; set; }

    /// <summary>
    /// Group
    /// </summary>
    public string Descriptor { get; set; }

    /// <summary>
    /// Contract size
    /// </summary>
    public double? Volume { get; set; }

    /// <summary>
    /// Size of partially filled contract
    /// </summary>
    public double? CurrentVolume { get; set; }

    /// <summary>
    /// Open price for the order
    /// </summary>
    public double? Price { get; set; }

    /// <summary>
    /// Time stamp
    /// </summary>
    public DateTime? Time { get; set; }

    /// <summary>
    /// Transaction type, e.g. withdrawal or order placement
    /// </summary>
    public OperationEnum? Operation { get; set; }

    /// <summary>
    /// Status of the order, e.g. Pending
    /// </summary>
    public OrderStatusEnum? Status { get; set; }

    /// <summary>
    /// Instrument to buy or sell
    /// </summary>
    public InstrumentModel Instrument { get; set; }

    /// <summary>
    /// Option
    /// </summary>
    public OptionModel Option { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public TransactionModel()
    {
      Id = $"{Guid.NewGuid()}";
      Time = DateTime.Now;
    }

    /// <summary>
    /// Clone
    /// </summary>
    public object Clone() => MemberwiseClone() as TransactionModel;
  }
}
