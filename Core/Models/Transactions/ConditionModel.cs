using System;
using System.Collections.Generic;
using Terminal.Core.Domains;

namespace Terminal.Core.Models
{
  public class ConditionModel : Dictionary<string, object>
  {
    /// <summary>
    /// Count
    /// </summary>
    public virtual int? Span { get; set; }

    /// <summary>
    /// Start date
    /// </summary>
    public virtual double? MinPrice { get; set; }

    /// <summary>
    /// End date
    /// </summary>
    public virtual double? MaxPrice { get; set; }

    /// <summary>
    /// Start date
    /// </summary>
    public virtual DateTime? MinDate { get; set; }

    /// <summary>
    /// End date
    /// </summary>
    public virtual DateTime? MaxDate { get; set; }

    /// <summary>
    /// Asset
    /// </summary>
    public virtual InstrumentModel Instrument { get; set; }
  }
}
