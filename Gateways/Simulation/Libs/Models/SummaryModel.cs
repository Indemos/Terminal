using Core.Models;
using System.Collections.Generic;

namespace Simulation.Models
{
  public record SummaryModel
  {
    /// <summary>
    /// Depth of market
    /// </summary>
    public DomModel Dom { get; init; }

    /// <summary>
    /// Reference to the instrument
    /// </summary>
    public InstrumentModel Instrument { get; init; }

    /// <summary>
    /// List of all ticks from the server
    /// </summary>
    public List<PriceModel> Points { get; init; } = [];

    /// <summary>
    /// List of all ticks from the server aggregated into bars
    /// </summary>
    public List<PriceModel> PointGroups { get; init; } = [];

    /// <summary>
    /// List of option contracts for the current point
    /// </summary>
    public List<InstrumentModel> Options { get; init; } = [];
  }
}
