using FluentValidation;
using System;

namespace Core.ModelSpace
{
  /// <summary>
  /// Generic model for time series
  /// </summary>
  public interface ITimeModel : IBaseModel
  {
    /// <summary>
    /// Last price or value
    /// </summary>
    double? Last { get; set; }

    /// <summary>
    /// Time stamp
    /// </summary>
    DateTime? Time { get; set; }

    /// <summary>
    /// Aggregation period for the quotes
    /// </summary>
    TimeSpan? TimeFrame { get; set; }
  }

  /// <summary>
  /// Model to keep a snapshot of some value at specified time
  /// </summary>
  public class TimeModel : BaseModel, ITimeModel
  {
    /// <summary>
    /// Last price or value
    /// </summary>
    public virtual double? Last { get; set; }

    /// <summary>
    /// Time stamp
    /// </summary>
    public virtual DateTime? Time { get; set; }

    /// <summary>
    /// Aggregation period for the quotes
    /// </summary>
    public virtual TimeSpan? TimeFrame { get; set; }
  }

  /// <summary>
  /// Validation rules
  /// </summary>
  public class TimeValidation : AbstractValidator<ITimeModel>
  {
    public TimeValidation()
    {
      RuleFor(o => o.Last).NotNull().NotEqual(0).WithMessage("No last price");
      RuleFor(o => o.Time).NotNull().WithMessage("No time");
      RuleFor(o => o.TimeFrame).NotNull().WithMessage("No time frame");
    }
  }
}
