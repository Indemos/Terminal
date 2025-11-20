using Core.Models;
using System;

namespace InteractiveBrokers.Models
{
  public record Connection
  {
    /// <summary>
    /// Port
    /// </summary>
    public int Port { get; init; }

    /// <summary>
    /// Host
    /// </summary>
    public string Host { get; init; }

    /// <summary>
    /// Throttle
    /// </summary>
    public TimeSpan Span { get; init; } = TimeSpan.Zero;

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
