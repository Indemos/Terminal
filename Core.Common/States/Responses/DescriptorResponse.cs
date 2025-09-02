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
    public List<string> Errors { get; init; } = [];
  }
}
