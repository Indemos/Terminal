using System.Collections.Generic;

namespace Core.Models
{
  public record PricesResponse : Response
  {
    /// <summary>
    /// Data
    /// </summary>
    public List<Price> Data { get; init; } = [];
  }
}
