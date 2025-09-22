using System.Collections.Generic;

namespace Core.Common.States
{
  public record DescriptorResponse
  {
    /// <summary>
    /// Data
    /// </summary>
    public string Data { get; init; }

    /// <summary>
    /// Errors
    /// </summary>
    public IList<string> Errors { get; init; } = [];
  }
}
