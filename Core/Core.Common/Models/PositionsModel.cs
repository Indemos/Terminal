using Core.Common.Grains;
using System.Collections.Generic;

namespace Core.Common.Models
{
  public record PositionsModel
  {
    /// <summary>
    /// Active orders
    /// </summary>
    public Dictionary<string, IPositionGrain> Grains { get; init; } = [];
  }
}
