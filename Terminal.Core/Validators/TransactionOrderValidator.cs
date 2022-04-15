using Terminal.Core.EnumSpace;
using Terminal.Core.MessageSpace;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using Terminal.Core.ModelSpace;

namespace Terminal.Core.ValidatorSpace
{
  /// <summary>
  /// Validation rules
  /// </summary>
  public class TransactionOrderValidator : AbstractValidator<ITransactionOrderModel>
  {
    public TransactionOrderValidator()
    {
      RuleFor(o => o.Instrument).NotNull().NotEmpty().WithMessage("No instrument");
      RuleFor(o => o.Size).NotNull().NotEqual(0).WithMessage("No size");
      RuleFor(o => o.Category).NotNull().NotEqual(OrderCategoryEnum.None).WithMessage("No side");
      RuleFor(o => o.Orders).NotNull().WithMessage("No orders");
    }
  }
}
