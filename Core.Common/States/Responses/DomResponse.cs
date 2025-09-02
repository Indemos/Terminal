using System.Collections.Generic;

namespace Core.Common.States
{
  public record DomResponse
  {
    /// <summary>
    /// Data
    /// </summary>
    public DomState Data { get; init; }

    /// <summary>
    /// Errors
    /// </summary>
    public List<string> Errors { get; init; } = [];
  }
}
