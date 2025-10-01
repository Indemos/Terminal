using Core.Grains;
using System.Collections.Generic;

namespace Core.Models
{
  public record PositionsModel
  {
    /// <summary>
    /// Active orders
    /// </summary>
    public Dictionary<string, IPositionGrain> Grains { get; init; } = [];
  }
}
