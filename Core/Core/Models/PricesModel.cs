using System.Collections.Generic;

namespace Core.Models
{
  public record PricesModel
  {
    /// <summary>
    /// Instrument
    /// </summary>
    public InstrumentModel Instrument { get; init; }

    /// <summary>
    /// List of all ticks from the server
    /// </summary>
    public IList<PriceModel> Prices { get; init; } = [];

    /// <summary>
    /// List of all ticks from the server aggregated into bars
    /// </summary>
    public IList<PriceModel> PriceGroups { get; init; } = [];
  }
}
