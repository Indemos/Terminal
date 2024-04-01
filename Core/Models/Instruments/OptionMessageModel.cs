using System;

namespace Terminal.Core.Models
{
  public struct OptionMessageModel
  {
    /// <summary>
    /// Symbol
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// End date
    /// </summary>
    public DateTime? MinDate { get; set; }

    /// <summary>
    /// Start date
    /// </summary>
    public DateTime? MaxDate { get; set; }
  }
}
