using System.Collections.Generic;

namespace Core.Common.States
{
  public record MetaState
  {
    /// <summary>
    /// Count
    /// </summary>
    public int? Count { get; init; }

    /// <summary>
    /// Start date
    /// </summary>
    public double? MinPrice { get; init; }

    /// <summary>
    /// End date
    /// </summary>
    public double? MaxPrice { get; init; }

    /// <summary>
    /// Start date
    /// </summary>
    public long? MinDate { get; init; }

    /// <summary>
    /// End date
    /// </summary>
    public long? MaxDate { get; init; }

    /// <summary>
    /// Asset
    /// </summary>
    public InstrumentState Instrument { get; init; }

    /// <summary>
    /// Criteria
    /// </summary>
    public Dictionary<string, double> Criteria { get; set; } = [];
  }
}
