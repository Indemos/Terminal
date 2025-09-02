using Orleans;
using System;

namespace Core.Common.States
{
  public record BarState
  {
    /// <summary>
    /// Lowest price of the bar
    /// </summary>
    public double? Low { get; init; }

    /// <summary>
    /// Highest price of the bar
    /// </summary>
    public double? High { get; init; }

    /// <summary>
    /// Open price of the bar
    /// </summary>
    public double? Open { get; init; }

    /// <summary>
    /// Close price of the bar
    /// </summary>
    public double? Close { get; init; }

    /// <summary>
    /// Time stamp
    /// </summary>
    public DateTime? Time { get; init; }
  }
}
