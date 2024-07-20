using System;

namespace Terminal.Core.Models
{
  public class BarModel : ICloneable
  {
    /// <summary>
    /// Lowest price of the bar
    /// </summary>
    public double? Low { get; set; }

    /// <summary>
    /// Highest price of the bar
    /// </summary>
    public double? High { get; set; }

    /// <summary>
    /// Open price of the bar
    /// </summary>
    public double? Open { get; set; }

    /// <summary>
    /// Close price of the bar
    /// </summary>
    public double? Close { get; set; }

    /// <summary>
    /// Clone
    /// </summary>
    public object Clone() => MemberwiseClone() as BarModel;
  }
}
