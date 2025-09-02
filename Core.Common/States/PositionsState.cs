using Core.Common.Grains;
using System.Collections.Generic;

namespace Core.Common.States
{
  public record PositionsState
  {
    /// <summary>
    /// Active orders
    /// </summary>
    public Dictionary<string, IPositionGrain> Grains { get; init; } = [];
  }
}
