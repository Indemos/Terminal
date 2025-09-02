using Core.Common.Enums;
using System.Collections.Generic;

namespace Core.Common.States
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
    public List<string> Errors { get; init; } = [];
  }
}
