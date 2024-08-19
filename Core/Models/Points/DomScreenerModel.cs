namespace Terminal.Core.Models
{
  public class DomScreenerModel
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
    /// Exchange
    /// </summary>
    public virtual string Exchange { get; set; }

    /// <summary>
    /// Currency
    /// </summary>
    public virtual string Currency { get; set; }
  }
}
