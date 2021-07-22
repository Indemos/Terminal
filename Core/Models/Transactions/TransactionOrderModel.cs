using Core.EnumSpace;
using Core.MessageSpace;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;

namespace Core.ModelSpace
{
  /// <summary>
  /// Generic order model
  /// </summary>
  public interface ITransactionOrderModel : ITransactionModel
  {
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
    IEnumerable<ITransactionOrderModel> Orders { get; set; }

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
    public virtual IEnumerable<ITransactionOrderModel> Orders { get; set; }

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

  /// <summary>
  /// Validation rules
  /// </summary>
  public class TransactionOrderValidation : AbstractValidator<ITransactionOrderModel>
  {
    public TransactionOrderValidation()
    {
      RuleFor(o => o.Instrument).NotNull().NotEmpty().WithMessage("No instrument");
      RuleFor(o => o.Size).NotNull().NotEqual(0).WithMessage("No size");
      RuleFor(o => o.Type).NotNull().NotEqual(TransactionTypeEnum.None).WithMessage("No side");
      RuleFor(o => o.Orders).NotNull().WithMessage("No orders");
    }
  }

  /// <summary>
  /// Validation rules for limit orders
  /// </summary>
  public class TransactionOrderPriceValidation : AbstractValidator<ITransactionOrderModel>
  {
    private static readonly List<TransactionTypeEnum?> _immediateTypes = new List<TransactionTypeEnum?>
    {
      TransactionTypeEnum.Buy,
      TransactionTypeEnum.Sell
    };

    public TransactionOrderPriceValidation()
    {
      Include(new TransactionOrderValidation());

      When(o => _immediateTypes.Contains(o.Type) == false, () => RuleFor(o => o.Price).NotNull().NotEqual(0).WithMessage("No open price"));
      When(o => Equals(o.Type, TransactionTypeEnum.BuyStop), () => RuleFor(o => o.Price).GreaterThanOrEqualTo(o => o.Instrument.Points.Last().Ask).WithMessage("Buy stop is below the offer"));
      When(o => Equals(o.Type, TransactionTypeEnum.SellStop), () => RuleFor(o => o.Price).LessThanOrEqualTo(o => o.Instrument.Points.Last().Bid).WithMessage("Sell stop is above the bid"));
      When(o => Equals(o.Type, TransactionTypeEnum.BuyLimit), () => RuleFor(o => o.Price).LessThanOrEqualTo(o => o.Instrument.Points.Last().Ask).WithMessage("Buy limit is above the offer"));
      When(o => Equals(o.Type, TransactionTypeEnum.SellLimit), () => RuleFor(o => o.Price).GreaterThanOrEqualTo(o => o.Instrument.Points.Last().Bid).WithMessage("Sell limit is below the bid"));
    }
  }
}
