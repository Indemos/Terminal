using System.Collections.Generic;

namespace Core.Models
{
  public record DomResponse
  {
    /// <summary>
    /// Data
    /// </summary>
    public Dom Data { get; init; }

    /// <summary>
    /// Errors
    /// </summary>
    public IList<string> Errors { get; init; } = [];
  }
}
