using System;
using Terminal.Core.Collections;
using Terminal.Core.Models;

namespace Terminal.Core.Domains
{
  public interface IInstrument
  {
    /// <summary>
    /// Name
    /// </summary>
    string Name { get; set; }

    /// <summary>
    /// Long swap rate for keeping position overnight
    /// </summary>
    decimal? SwapLong { get; set; }

    /// <summary>
    /// Short swap rate for keeping position overnight
    /// </summary>
    decimal? SwapShort { get; set; }

    /// <summary>
    /// Commission
    /// </summary>
    decimal? Commission { get; set; }

    /// <summary>
    /// Contract size for 1 trading lot in currencies and futures
    /// </summary>
    decimal? ContractSize { get; set; }

    /// <summary>
    /// Tick size, i.e. minimum price change 
    /// </summary>
    decimal? StepSize { get; set; }

    /// <summary>
    /// Tick value, i.e. how much price change within one tick
    /// </summary>
    decimal? StepValue { get; set; }

    /// <summary>
    /// Aggregation period for the quotes
    /// </summary>
    TimeSpan? TimeFrame { get; set; }

    /// <summary>
    /// List of all ticks from the server
    /// </summary>
    ObservableGroupCollection<PointModel> Points { get; set; }

    /// <summary>
    /// List of all ticks from the server aggregated into bars
    /// </summary>
    ObservableGroupCollection<PointModel> PointGroups { get; set; }
  }

  public class Instrument : IInstrument
  {
    /// <summary>
    /// Name
    /// </summary>
    public virtual string Name { get; set; }

    /// <summary>
    /// Long swap rate for keeping position overnight
    /// </summary>
    public virtual decimal? SwapLong { get; set; }

    /// <summary>
    /// Short swap rate for keeping position overnight
    /// </summary>
    public virtual decimal? SwapShort { get; set; }

    /// <summary>
    /// Commission
    /// </summary>
    public virtual decimal? Commission { get; set; }

    /// <summary>
    /// Contract size for 1 trading lot in currencies and futures
    /// </summary>
    public virtual decimal? ContractSize { get; set; }

    /// <summary>
    /// Tick size, i.e. minimum price change 
    /// </summary>
    public virtual decimal? StepSize { get; set; }

    /// <summary>
    /// Tick value, i.e. how much price change within one tick
    /// </summary>
    public virtual decimal? StepValue { get; set; }

    /// <summary>
    /// Aggregation period for the quotes
    /// </summary>
    public virtual TimeSpan? TimeFrame { get; set; }

    /// <summary>
    /// List of all ticks from the server
    /// </summary>
    public virtual ObservableGroupCollection<PointModel> Points { get; set; }

    /// <summary>
    /// List of all ticks from the server aggregated into bars
    /// </summary>
    public virtual ObservableGroupCollection<PointModel> PointGroups { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public Instrument()
    {
      SwapLong = 0;
      SwapShort = 0;
      StepSize = 0.01m;
      StepValue = 0.01m;
      Commission = 0;
      ContractSize = 1;

      Points = [];
      PointGroups = [];
    }
  }
}
