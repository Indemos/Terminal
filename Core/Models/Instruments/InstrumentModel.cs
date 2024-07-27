using System;
using Terminal.Core.Collections;
using Terminal.Core.Models;

namespace Terminal.Core.Domains
{
  public class InstrumentModel : ICloneable
  {
    /// <summary>
    /// Name
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Instrument type
    /// </summary>
    public string Security { get; set; }

    /// <summary>
    /// Exchange
    /// </summary>
    public string Exchange { get; set; }

    /// <summary>
    /// Commission
    /// </summary>
    public double? Commission { get; set; }

    /// <summary>
    /// Contract size for 1 trading lot in currencies and futures
    /// </summary>
    public double? ContractSize { get; set; }

    /// <summary>
    /// Contract size
    /// </summary>
    public double? Leverage { get; set; }

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
    /// Base currency contract
    /// </summary>
    public CurrencyModel Currency { get; set; }

    /// <summary>
    /// List of all ticks from the server
    /// </summary>
    public ObservableGroupCollection<PointModel> Points { get; set; }

    /// <summary>
    /// List of all ticks from the server aggregated into bars
    /// </summary>
    public ObservableGroupCollection<PointModel> PointGroups { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public InstrumentModel()
    {
      Leverage = 1;
      StepSize = 0.01;
      StepValue = 0.01;
      Commission = 0;
      ContractSize = 1;

      Points = [];
      PointGroups = [];
    }

    /// <summary>
    /// Clone
    /// </summary>
    public object Clone() => MemberwiseClone() as InstrumentModel;
  }
}
