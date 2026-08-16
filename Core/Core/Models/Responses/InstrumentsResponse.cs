using System.Collections.Generic;

namespace Core.Models
{
  public record InstrumentsResponse : Response
  {
    /// <summary>
    /// Data
    /// </summary>
    public List<Instrument> Data { get; init; } = [];
  }
}
