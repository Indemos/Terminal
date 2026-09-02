using Core.Enums;

namespace Core.Models
{
  public record DomOrder
  {
    /// <summary>
    /// Id
    /// </summary>
    public string Id { get; init; }

    /// <summary>
    /// Name
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Source of order
    /// </summary>
    public string Source { get; init; }

    /// <summary>
    /// Flags
    /// </summary>
    public int? Mask { get; init; }

    /// <summary>
    /// Sequence number
    /// </summary>
    public int? Index { get; init; }

    /// <summary>
    /// Size
    /// </summary>
    public double? Size { get; init; }

    /// <summary>
    /// Price
    /// </summary>
    public double? Price { get; init; }

    /// <summary>
    /// Side
    /// </summary>
    public DomSide? Side { get; init; }

    /// <summary>
    /// Action
    /// </summary>
    public DomAction? Action { get; init; }
  }
}
