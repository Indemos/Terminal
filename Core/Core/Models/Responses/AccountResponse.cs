using System.Collections.Generic;

namespace Core.Models
{
  public record AccountResponse
  {
    /// <summary>
    /// Data
    /// </summary>
    public AccountModel Data { get; init; }

    /// <summary>
    /// Errors
    /// </summary>
    public IList<string> Errors { get; init; } = [];
  }
}
