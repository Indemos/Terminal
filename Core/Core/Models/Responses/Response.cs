using System.Collections.Generic;

namespace Core.Models
{
  public record Response
  {
    /// <summary>
    /// Errors
    /// </summary>
    public List<string> Errors { get; init; } = [];
  }
}
