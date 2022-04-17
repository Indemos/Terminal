using System;
using Terminal.Core.CollectionSpace;

namespace Terminal.Core.ModelSpace
{
  /// <summary>
  /// Generic instrument definition
  /// </summary>
  public interface IInstrumentModel : IBaseModel
  {
    /// <summary>
    /// Bid price
    /// </summary>
    double? Bid { get; set; }

    /// <summary>
    /// Ask price
    /// </summary>
    double? Ask { get; set; }

    /// <summary>
    /// Current price
    /// </summary>
    double? Price { get; set; }

    /// <summary>
    /// Bid volume
    /// </summary>
    double? BidSize { get; set; }

    /// <summary>
    /// Ask volume
    /// </summary>
    double? AskSize { get; set; }

    /// <summary>
    /// Overal volume
    /// </summary>
    double? Size { get; set; }

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
    IIndexCollection<IPointModel> Points { get; set; }

    /// <summary>
    /// List of all ticks from the server aggregated into bars
    /// </summary>
    ITimeCollection<IPointModel> PointGroups { get; set; }
  }

  /// <summary>
  /// Generic instrument definition
  /// </summary>
  public class InstrumentModel : BaseModel, IInstrumentModel
  {
    /// <summary>
    /// Bid price
    /// </summary>
    public virtual double? Bid { get; set; }

    /// <summary>
    /// Ask price
    /// </summary>
    public virtual double? Ask { get; set; }

    /// <summary>
    /// Current price
    /// </summary>
    public virtual double? Price { get; set; }

    /// <summary>
    /// Bid volume
    /// </summary>
    public virtual double? BidSize { get; set; }

    /// <summary>
    /// Ask volume
    /// </summary>
    public virtual double? AskSize { get; set; }

    /// <summary>
    /// Overal volume
    /// </summary>
    public virtual double? Size { get; set; }

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
    public virtual IIndexCollection<IPointModel> Points { get; set; }

    /// <summary>
    /// List of all ticks from the server aggregated into bars
    /// </summary>
    public virtual ITimeCollection<IPointModel> PointGroups { get; set; }

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

      Points = new IndexCollection<IPointModel>();
      PointGroups = new TimeGroupCollection<IPointModel>();
    }
  }
}
