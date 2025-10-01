using Core.Grains;
using System.Collections.Generic;

namespace Core.Models
{
  public record OrdersModel
  {
    /// <summary>
    /// Active orders
    /// </summary>
    public Dictionary<string, IOrderGrain> Grains { get; init; } = [];
  }
}
