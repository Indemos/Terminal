using System;
using System.Collections.Generic;

namespace Terminal.Core.Models
{
  public class SnapshotModel : ICloneable
  {
    /// <summary>
    /// Point
    /// </summary>
    public virtual PointModel Point { get; set; }

    /// <summary>
    /// List of option contracts for the current point
    /// </summary>
    public virtual IList<OptionModel> Options { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public SnapshotModel()
    {
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
