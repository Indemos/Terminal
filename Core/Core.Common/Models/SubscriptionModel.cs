using Core.Common.Enums;

namespace Core.Common.Models
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
