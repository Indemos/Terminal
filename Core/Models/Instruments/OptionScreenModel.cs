using System;

namespace Terminal.Core.Models
{
  public class OptionScreenModel
  {
    /// <summary>
    /// Symbol name
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Min strike
    /// </summary>
    public double? MinPrice { get; set; }

    /// <summary>
    /// Max strike
    /// </summary>
    public double? MaxPrice { get; set; }

    /// <summary>
    /// Start date
    /// </summary>
    public DateTime MinDate { get; set; }

    /// <summary>
    /// End date
    /// </summary>
    public DateTime MaxDate { get; set; }
  }
}
