using Core.Enums;
using System;

namespace Core.Models
{
  public record Message
  {
    /// <summary>
    /// Message or error code
    /// </summary>
    public int? Code { get; init; }

    /// <summary>
    /// Description
    /// </summary>
    public string Source { get; init; }

    /// <summary>
    /// Description
    /// </summary>
    public string Description { get; init; }

    /// <summary>
    /// Action
    /// </summary>
    public ActionEnum Action { get; init; }

    /// <summary>
    /// Exception
    /// </summary>
    public Exception Error { get; init; }
  }
}
