using System;

namespace Terminal.Core.Models
{
  public class PointScreenerModel
  {
    /// <summary>
    /// Symbol name
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Security type
    /// </summary>
    public string Security { get; set; }

    /// <summary>
    /// Start date
    /// </summary>
    public DateTime? MinDate { get; set; }

    /// <summary>
    /// End date
    /// </summary>
    public DateTime? MaxDate { get; set; }
  }
}
