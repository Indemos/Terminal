using FluentValidation;
using System.Collections.Generic;

namespace Core.ModelSpace
{
  /// <summary>
  /// Definition
  /// </summary>
  public interface IPointModel : ITimeModel
  {
    /// <summary>
    /// Bid
    /// </summary>
    double? Bid { get; set; }

    /// <summary>
    /// Ask
    /// </summary>
    double? Ask { get; set; }

    /// <summary>
    /// Size of the bid on the current tick
    /// </summary>
    double? BidSize { get; set; }

    /// <summary>
    /// Size of the ask on the current tick
    /// </summary>
    double? AskSize { get; set; }

    /// <summary>
    /// Reference to the chart properties
    /// </summary>
    IChartModel Chart { get; set; }

    /// <summary>
    /// Rference to the complex data point
    /// </summary>
    IPointBarModel Bar { get; set; }

    /// <summary>
    /// Reference to the account
    /// </summary>
    IAccountModel Account { get; set; }

    /// <summary>
    /// Reference to the instrument
    /// </summary>
    IInstrumentModel Instrument { get; set; }

    /// <summary>
    /// Values from related series synced with the current bar, e.g. averaged indicator calculations for the charts
    /// </summary>
    Dictionary<string, IPointModel> Series { get; set; }
  }

  /// <summary>
  /// Implementation
  /// </summary>
  public class PointModel : TimeModel, IPointModel
  {
    /// <summary>
    /// Bid
    /// </summary>
    public virtual double? Bid { get; set; }

    /// <summary>
    /// Ask
    /// </summary>
    public virtual double? Ask { get; set; }

    /// <summary>
    /// Volume of the bid 
    /// </summary>
    public virtual double? BidSize { get; set; }

    /// <summary>
    /// Volume of the ask
    /// </summary>
    public virtual double? AskSize { get; set; }

    /// <summary>
    /// Reference to the chart properties
    /// </summary>
    public virtual IChartModel Chart { get; set; }

    /// <summary>
    /// Rference to the complex data point
    /// </summary>
    public virtual IPointBarModel Bar { get; set; }

    /// <summary>
    /// Reference to the account
    /// </summary>
    public virtual IAccountModel Account { get; set; }

    /// <summary>
    /// Reference to the instrument
    /// </summary>
    public virtual IInstrumentModel Instrument { get; set; }

    /// <summary>
    /// Values from related series synced with the current data point, e.g. averaged indicator calculations for the charts
    /// </summary>
    public virtual Dictionary<string, IPointModel> Series { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public PointModel()
    {
      Chart = new ChartModel();
      Series = new Dictionary<string, IPointModel>();
    }
  }

  /// <summary>
  /// Validation rules
  /// </summary>
  public class PointValidation : AbstractValidator<IPointModel>
  {
    public PointValidation()
    {
      RuleFor(o => o.Bid).NotNull().NotEqual(0).WithMessage("No bid");
      RuleFor(o => o.Ask).NotNull().NotEqual(0).WithMessage("No offer");
      RuleFor(o => o.BidSize).NotNull().WithMessage("No bid size");
      RuleFor(o => o.AskSize).NotNull().WithMessage("No offer size");
      RuleFor(o => o.Account).NotNull().WithMessage("No account");
      RuleFor(o => o.Instrument).NotNull().WithMessage("No instrument");
      RuleFor(o => o.Series).NotNull().WithMessage("No series");
    }
  }

  /// <summary>
  /// Validation rules
  /// </summary>
  public class PointCollectionsValidation : AbstractValidator<IPointModel>
  {
    public PointCollectionsValidation()
    {
      Include(new PointValidation());

      RuleFor(o => o.Series).NotNull().NotEmpty().WithMessage("No series");
    }
  }
}
