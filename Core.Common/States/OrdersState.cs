using Core.Common.Grains;
using Orleans;
using System.Collections.Generic;

namespace Core.Common.States
{
  public record OrdersState
  {
    /// <summary>
    /// Active orders
    /// </summary>
    public Dictionary<string, IOrderGrain> Grains { get; init; } = new();
  }
}
