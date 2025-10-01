using Core.Enums;

namespace Core.Models
{
  public record SubscriptionModel
  {
    /// <summary>
    /// Next state
    /// </summary>
    public SubscriptionEnum Next { get; init; }

    /// <summary>
    /// Previous state
    /// </summary>
    public SubscriptionEnum Previous { get; init; }
  }
}
