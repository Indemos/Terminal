using Core.Enums;
using System.Collections.Generic;

namespace Core.Models
{
  public record StatusResponse
  {
    /// <summary>
    /// Data
    /// </summary>
    public StatusEnum? Data { get; init; }

    /// <summary>
    /// Errors
    /// </summary>
    public IList<string> Errors { get; init; } = [];
  }
}
