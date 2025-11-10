using System;
using System.Collections.Generic;

namespace Core.Models
{
  public record Criteria
  {
    /// <summary>
    /// Count
    /// </summary>
    public int? Count { get; init; }

    /// <summary>
    /// Start date
    /// </summary>
    public double? MinPrice { get; init; }

    /// <summary>
    /// End date
    /// </summary>
    public double? MaxPrice { get; init; }

    /// <summary>
    /// Start date
    /// </summary>
    public DateTime? MinDate { get; init; }

    /// <summary>
    /// End date
    /// </summary>
    public DateTime? MaxDate { get; init; }

    /// <summary>
    /// Account
    /// </summary>
    public Account Account { get; init; }

    /// <summary>
    /// Instrument
    /// </summary>
    public Instrument Instrument { get; init; }

    /// <summary>
    /// Criteria
    /// </summary>
    public Dictionary<string, string> Data { get; set; } = [];
  }
}
