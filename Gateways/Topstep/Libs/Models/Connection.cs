using Core.Models;
using System;

namespace Topstep.Models
{
  public record Connection
  {
    /// <summary>
    /// Username
    /// </summary>
    public string Username { get; init; }

    /// <summary>
    /// Access token
    /// </summary>
    public string Token { get; init; }

    /// <summary>
    /// Timeout
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Account
    /// </summary>
    public Account Account { get; init; }
  }
}
