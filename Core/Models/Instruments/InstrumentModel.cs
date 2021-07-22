using Core.CollectionSpace;
using FluentValidation;
using System;

namespace Core.ModelSpace
{
  /// <summary>
  /// Generic instrument definition
  /// </summary>
  public interface IInstrumentModel : IBaseModel
  {
    /// <summary>
    /// Long swap rate for keeping position overnight
    /// </summary>
    double? SwapLong { get; set; }

    /// <summary>
    /// Short swap rate for keeping position overnight
    /// </summary>
    double? SwapShort { get; set; }

    /// <summary>
    /// Commission
    /// </summary>
    double? Commission { get; set; }

    /// <summary>
    /// Contract size for 1 trading lot in currencies and futures
    /// </summary>
    double? ContractSize { get; set; }

    /// <summary>
    /// Tick size, i.e. minimum price change 
    /// </summary>
    double? StepSize { get; set; }

    /// <summary>
    /// Tick value, i.e. how much price change within one tick
    /// </summary>
    double? StepValue { get; set; }

    /// <summary>
    /// Aggregation period for the quotes
    /// </summary>
    TimeSpan? TimeFrame { get; set; }

    /// <summary>
    /// Reference to the chart properties
    /// </summary>
    IChartModel Chart { get; set; }

    /// <summary>
    /// Reference to the account
    /// </summary>
    IAccountModel Account { get; set; }

    /// <summary>
    /// Reference to option model
    /// </summary>
    IInstrumentOptionModel Option { get; set; }

    /// <summary>
    /// Reference to future model
    /// </summary>
    IInstrumentFutureModel Future { get; set; }

    /// <summary>
    /// List of all ticks from the server
    /// </summary>
    ITimeSpanCollection<IPointModel> Points { get; set; }

    /// <summary>
    /// List of all ticks from the server aggregated into bars
    /// </summary>
    ITimeSpanCollection<IPointModel> PointGroups { get; set; }
  }

  /// <summary>
  /// Generic instrument definition
  /// </summary>
  public class InstrumentModel : BaseModel, IInstrumentModel
  {
    /// <summary>
    /// Long swap rate for keeping position overnight
    /// </summary>
    public virtual double? SwapLong { get; set; }

    /// <summary>
    /// Short swap rate for keeping position overnight
    /// </summary>
    public virtual double? SwapShort { get; set; }

    /// <summary>
    /// Commission
    /// </summary>
    public virtual double? Commission { get; set; }

    /// <summary>
    /// Contract size for 1 trading lot in currencies and futures
    /// </summary>
    public virtual double? ContractSize { get; set; }

    /// <summary>
    /// Tick size, i.e. minimum price change 
    /// </summary>
    public virtual double? StepSize { get; set; }

    /// <summary>
    /// Tick value, i.e. how much price change within one tick
    /// </summary>
    public virtual double? StepValue { get; set; }

    /// <summary>
    /// Aggregation period for the quotes
    /// </summary>
    public virtual TimeSpan? TimeFrame { get; set; }

    /// <summary>
    /// Reference to the chart properties
    /// </summary>
    public virtual IChartModel Chart { get; set; }

    /// <summary>
    /// Reference to the account
    /// </summary>
    public virtual IAccountModel Account { get; set; }

    /// <summary>
    /// Reference to option model
    /// </summary>
    public virtual IInstrumentOptionModel Option { get; set; }

    /// <summary>
    /// Reference to future model
    /// </summary>
    public virtual IInstrumentFutureModel Future { get; set; }

    /// <summary>
    /// List of all ticks from the server
    /// </summary>
    public virtual ITimeSpanCollection<IPointModel> Points { get; set; }

    /// <summary>
    /// List of all ticks from the server aggregated into bars
    /// </summary>
    public virtual ITimeSpanCollection<IPointModel> PointGroups { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public InstrumentModel()
    {
      SwapLong = 0.0;
      SwapShort = 0.0;
      StepSize = 0.01;
      StepValue = 0.01;
      Commission = 0.0;
      ContractSize = 1.0;

      Chart = new ChartModel();
      Points = new TimeSpanCollection<IPointModel>();
      PointGroups = new TimeSpanCollection<IPointModel>();
    }
  }

  /// <summary>
  /// Validation rules
  /// </summary>
  public class InstrumentValidation : AbstractValidator<IInstrumentModel>
  {
    public InstrumentValidation()
    {
      RuleFor(o => o.SwapLong).NotNull().WithMessage("No long swap");
      RuleFor(o => o.SwapShort).NotNull().WithMessage("No short swap");
      RuleFor(o => o.Commission).NotNull().WithMessage("No commission");
      RuleFor(o => o.ContractSize).NotNull().NotEqual(0).WithMessage("No contract size");
      RuleFor(o => o.StepSize).NotNull().NotEqual(0).WithMessage("No point size");
      RuleFor(o => o.StepValue).NotNull().NotEqual(0).WithMessage("No point value");
      RuleFor(o => o.TimeFrame).NotNull().WithMessage("No time frame");
      RuleFor(o => o.Points).NotNull().WithMessage("No points");
      RuleFor(o => o.PointGroups).NotNull().WithMessage("No point groups");
    }
  }

  /// <summary>
  /// Validation rules
  /// </summary>
  public class InstrumentCollectionsValidation : AbstractValidator<IInstrumentModel>
  {
    public InstrumentCollectionsValidation()
    {
      Include(new InstrumentValidation());

      RuleFor(o => o.Points).NotNull().NotEmpty().WithMessage("No points");
      RuleFor(o => o.PointGroups).NotNull().NotEmpty().WithMessage("No point groups");
    }
  }
}
