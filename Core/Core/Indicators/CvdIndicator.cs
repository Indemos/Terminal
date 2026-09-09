using Core.Enums;
using Core.Models;

namespace Core.Indicators
{
  /// <summary>
  /// Cumulative Volume Delta (CVD) Indicator
  /// </summary>
  public class CvdIndicator
  {
    /// <summary>
    /// The current cumulative volume delta value.
    /// </summary>
    public virtual double Value { get; protected set; }

    /// <summary>
    /// Calculate
    /// </summary>
    /// <param name="order">The DOM order event</param>
    /// <returns>The updated CVD value</returns>
    public virtual double Update(DomOrder order)
    {
      // 1. Must be Trade only. Ignore resting order additions / modifications / cancels.
      if (order.Action is DomAction.Trade is false || order.Size is null)
      {
        return Value;
      }

      // 2. Update the global running total
      switch (order.Side)
      {
        // Ask = Aggressive Buyer = Positive Delta
        // Bid = Aggressive Seller = Negative Delta
        case DomSide.Ask: Value += order.Size.Value; break;
        case DomSide.Bid: Value -= order.Size.Value; break;
      }

      return Value;
    }
  }
}
