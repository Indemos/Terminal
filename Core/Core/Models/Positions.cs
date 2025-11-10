using Core.Grains;
using System.Collections.Generic;

namespace Core.Models
{
  public record Positions
  {
    /// <summary>
    /// Active orders
    /// </summary>
    public Dictionary<string, IPositionGrain> Grains { get; init; } = [];
  }
}
