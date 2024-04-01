using System;
using Terminal.Core.Domains;
using Terminal.Core.Enums;

namespace Terminal.Core.Models
{
  public struct TransactionModel
  {
    /// <summary>
    /// Id
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Contract size
    /// </summary>
    public double? Volume { get; set; }

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
    public IInstrument Instrument { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public TransactionModel()
    {
      Id = $"{Guid.NewGuid()}";
      Time = DateTime.Now;
    }
  }
}
