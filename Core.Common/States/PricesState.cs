using System.Collections.Generic;

namespace Core.Common.States
{
  public record PricesState
  {
    /// <summary>
    /// List of all ticks from the server
    /// </summary>
    public List<PriceState> Prices { get; init; } = [];

    /// <summary>
    /// List of all ticks from the server aggregated into bars
    /// </summary>
    public List<PriceState> PriceGroups { get; init; } = [];
  }
}
