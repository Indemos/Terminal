using System;

namespace Terminal.Core.Models
{
  public class OptionMessageModel
  {
    /// <summary>
    /// Symbol
    /// </summary>
    public virtual string Name { get; set; }

    /// <summary>
    /// End date
    /// </summary>
    public virtual DateTime? MinDate { get; set; }

    /// <summary>
    /// Start date
    /// </summary>
    public virtual DateTime? MaxDate { get; set; }
  }
}
