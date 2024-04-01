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
    /// List of all ticks from the server
    /// </summary>
    ObservableTimeCollection Points { get; set; }

    /// <summary>
    /// List of all ticks from the server aggregated into bars
    /// </summary>
    ObservableTimeCollection PointGroups { get; set; }
  }

  public class Instrument : IInstrument
  {
    /// <summary>
    /// Name
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Long swap rate for keeping position overnight
    /// </summary>
    public double? SwapLong { get; set; }

    /// <summary>
    /// Short swap rate for keeping position overnight
    /// </summary>
    public double? SwapShort { get; set; }

    /// <summary>
    /// Commission
    /// </summary>
    public double? Commission { get; set; }

    /// <summary>
    /// Contract size for 1 trading lot in currencies and futures
    /// </summary>
    public double? ContractSize { get; set; }

    /// <summary>
    /// Tick size, i.e. minimum price change 
    /// </summary>
    public double? StepSize { get; set; }

    /// <summary>
    /// Tick value, i.e. how much price change within one tick
    /// </summary>
    public double? StepValue { get; set; }

    /// <summary>
    /// Aggregation period for the quotes
    /// </summary>
    public TimeSpan? TimeFrame { get; set; }

    /// <summary>
    /// List of all ticks from the server
    /// </summary>
    public ObservableTimeCollection Points { get; set; }

    /// <summary>
    /// List of all ticks from the server aggregated into bars
    /// </summary>
    public ObservableTimeCollection PointGroups { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public Instrument()
    {
      SwapLong = 0.0;
      SwapShort = 0.0;
      StepSize = 0.01;
      StepValue = 0.01;
      Commission = 0.0;
      ContractSize = 1.0;

      Points = new ObservableTimeCollection();
      PointGroups = new ObservableTimeCollection();
    }
  }
}
