using System.Collections.Generic;

namespace Core.Common.States
{
  public record AccountResponse
  {
    /// <summary>
    /// Data
    /// </summary>
    public AccountState Data { get; init; }

    /// <summary>
    /// Errors
    /// </summary>
    public IList<string> Errors { get; init; } = [];
  }
}
