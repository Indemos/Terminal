using System;
using System.Collections.Generic;
using Terminal.Core.Enums;

namespace Terminal.Core.Models
{
  public class PointMessageModel
  {
    /// <summary>
    /// End date
    /// </summary>
    public virtual DateTime? MinDate { get; set; }

    /// <summary>
    /// Start date
    /// </summary>
    public virtual DateTime? MaxDate { get; set; }

    /// <summary>
    /// Resolution
    /// </summary>
    public virtual ResolutionEnum? Resolution { get; set; }

    /// <summary>
    /// Symbol
    /// </summary>
    public virtual IList<string> Names { get; set; }
  }
}
