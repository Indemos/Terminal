using System;

namespace Terminal.Core.Models
{
  public class PointScreenerModel
  {
    /// <summary>
    /// Symbol name
    /// </summary>
    public virtual string Name { get; set; }

    /// <summary>
    /// Security type
    /// </summary>
    public virtual string Security { get; set; }

    /// <summary>
    /// Start date
    /// </summary>
    public virtual DateTime? MinDate { get; set; }

    /// <summary>
    /// End date
    /// </summary>
    public virtual DateTime? MaxDate { get; set; }
  }
}
