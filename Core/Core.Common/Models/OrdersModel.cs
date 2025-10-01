using Core.Common.Grains;
using System.Collections.Generic;

namespace Core.Common.Models
{
  public record OrdersModel
  {
    /// <summary>
    /// Active orders
    /// </summary>
    public Dictionary<string, IOrderGrain> Grains { get; init; } = [];
  }
}
