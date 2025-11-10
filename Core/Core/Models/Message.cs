using Core.Enums;
using System;

namespace Core.Models
{
  public record Message
  {
    /// <summary>
    /// Event type
    /// </summary>
    public ActionEnum Action { get; init; }

    /// <summary>
    /// Message or error code
    /// </summary>
    public int? Code { get; init; }

    /// <summary>
    /// Description
    /// </summary>
    public string Content { get; init; }

    /// <summary>
    /// Exception
    /// </summary>
    public Exception Error { get; init; }
  }
}
