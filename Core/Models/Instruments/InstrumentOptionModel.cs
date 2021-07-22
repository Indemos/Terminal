using System;
using Core.EnumSpace;
using FluentValidation;

namespace Core.ModelSpace
{
  /// <summary>
  /// Definition
  /// </summary>
  public interface IInstrumentOptionModel : IBaseModel
  {
    /// <summary>
    /// Strike price
    /// </summary>
    double? Strike { get; set; }

    /// <summary>
    /// CALL or PUT
    /// </summary>
    OptionSideEnum? Side { get; set; }

    /// <summary>
    /// Expiration date
    /// </summary>
    DateTime? ExpirationDate { get; set; }
  }

  /// <summary>
  /// Implementation
  /// </summary>
  public class InstrumentOptionModel : BaseModel, IInstrumentOptionModel
  {
    /// <summary>
    /// Strike price
    /// </summary>
    public virtual double? Strike { get; set; }

    /// <summary>
    /// CALL or PUT
    /// </summary>
    public virtual OptionSideEnum? Side { get; set; }

    /// <summary>
    /// Expiration date
    /// </summary>
    public virtual DateTime? ExpirationDate { get; set; }
  }

  /// <summary>
  /// Validation rules
  /// </summary>
  public class InstrumentOptionValidation : AbstractValidator<IInstrumentOptionModel>
  {
    public InstrumentOptionValidation()
    {
      RuleFor(o => o.Side).NotNull().WithMessage("No side");
      RuleFor(o => o.Strike).NotNull().NotEqual(0).WithMessage("No strike");
      RuleFor(o => o.ExpirationDate).NotNull().WithMessage("No expiration date");
    }
  }
}
