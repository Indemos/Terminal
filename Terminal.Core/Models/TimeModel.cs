using System;

namespace Terminal.Core.ModelSpace
{
  /// <summary>
  /// Generic model for time series
  /// </summary>
  public interface ITimeModel : IBaseModel
  {
    /// <summary>
    /// Last price or value
    /// </summary>
    double? Last { get; set; }

    /// <summary>
    /// Time stamp
    /// </summary>
    DateTime? Time { get; set; }

    /// <summary>
    /// Aggregation period for the quotes
    /// </summary>
    TimeSpan? TimeFrame { get; set; }
  }

  /// <summary>
  /// Model to keep a snapshot of some value at specified time
  /// </summary>
  public class TimeModel : BaseModel, ITimeModel
  {
    /// <summary>
    /// Last price or value
    /// </summary>
    public virtual double? Last { get; set; }

    /// <summary>
    /// Time stamp
    /// </summary>
    public virtual DateTime? Time { get; set; }

    /// <summary>
    /// Aggregation period for the quotes
    /// </summary>
    public virtual TimeSpan? TimeFrame { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public TimeModel()
    {
      Time = DateTime.Now;
    }
  }
}
