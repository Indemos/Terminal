using System;

namespace Terminal.Core.Models
{
  public class BarModel : ICloneable
  {
    /// <summary>
    /// Lowest price of the bar
    /// </summary>
    public virtual decimal? Low { get; set; }

    /// <summary>
    /// Highest price of the bar
    /// </summary>
    public virtual decimal? High { get; set; }

    /// <summary>
    /// Open price of the bar
    /// </summary>
    public virtual decimal? Open { get; set; }

    /// <summary>
    /// Close price of the bar
    /// </summary>
    public virtual decimal? Close { get; set; }

    /// <summary>
    /// Clone
    /// </summary>
    public virtual object Clone() => MemberwiseClone() as BarModel;
  }
}
