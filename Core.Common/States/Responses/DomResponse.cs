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
    public IList<string> Errors { get; init; } = [];
  }
}
