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
    public TimeSpan Span { get; init; }

    /// <summary>
    /// Timeout
    /// </summary>
    public TimeSpan Timeout { get; init; }

    /// <summary>
    /// Account
    /// </summary>
    public Account Account { get; init; }
  }
}
