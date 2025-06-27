using System;
using System.Collections.Generic;
using Terminal.Core.Collections;
using Terminal.Core.Domains;
using Terminal.Core.Enums;

namespace Terminal.Core.Models
{
  public class SummaryModel : ICloneable
  {
    /// <summary>
    /// Status
    /// </summary>
    public virtual StatusEnum? Status { get; set; }

    /// <summary>
    /// Depth of market
    /// </summary>
    public virtual DomModel Dom { get; set; }

    /// <summary>
    /// Reference to the instrument
    /// </summary>
    public virtual InstrumentModel Instrument { get; set; }

    /// <summary>
    /// List of option contracts for the current point
    /// </summary>
    public virtual IList<InstrumentModel> Options { get; set; }

    /// <summary>
    /// List of all ticks from the server
    /// </summary>
    public virtual ConcurrentGroup<PointModel> Points { get; set; }

    /// <summary>
    /// List of all ticks from the server aggregated into bars
    /// </summary>
    public virtual ConcurrentGroup<PointModel> PointGroups { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public SummaryModel()
    {
      Points = [];
      PointGroups = [];
      Options = [];
    }

    /// <summary>
    /// Clone
    /// </summary>
    /// <returns></returns>
    public virtual object Clone()
    {
      var clone = MemberwiseClone() as PointModel;

      return clone;
    }
  }
}
