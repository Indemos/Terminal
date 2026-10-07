using Core.Models;

namespace Core.Indicators;

public class EmaIndicator
{
  // alpha = 2 / (n+1)
  protected double W => 2.0 / (Period + 1);
  // Count of bars including forming
  protected int count;
  // Sum of first Period for SMA seed
  protected double sum;
  // EMA of last closed bar - seed for Wilder
  protected double previousEma;
  // Time of forming bar
  protected long? currentTime;
  // Value of forming bar to replace
  protected double currentValue;
  // First bar seen
  protected bool setup;

  /// <summary>
  /// Period for EMA calculation
  /// </summary>
  public virtual int Period { get; set; } = 15;

  // Current EMA
  public virtual double Value { get; protected set; }

  /// <summary>
  /// Update EMA with new price point
  /// </summary>
  /// <param name="stamp"></param>
  /// <param name="point"></param>
  public virtual double? Update(long stamp, Price point)
  {
    // Extract price
    var price = point.Last.Value;

    // Same bar -> replace
    if (setup && currentTime == stamp)
    {
      // Recompute EMA with same prevClosedEma but new price
      if (count <= Period)
      {
        // Still in SMA seed phase: replace in sum
        sum += price - currentValue;
        Value = sum / count;
      }
      else
      {
        // EMA phase: EMA = alpha * price + (1 - alpha) * previousEma
        Value = W * price + (1 - W) * previousEma;
      }

      // Update forming value
      currentValue = price;

      return Value;
    }

    // New bar -> previous forming bar is now closed
    if (setup)
    {
      // Freeze its EMA as previous closed EMA if we finished seed
      if (count >= Period)
      {
        // This becomes the seed for next bar's EMA
        previousEma = Value;
      }
    }

    // New bar
    count++;

    // Build initial SMA seed
    if (count <= Period)
    {
      // Accumulate sum
      sum += price;
      // Seed EMA = SMA
      Value = sum / count;
    }
    else
    {
      // Wilder EMA
      Value = W * price + (1 - W) * previousEma;
    }

    // Store forming bar info
    currentTime = stamp;
    currentValue = price;
    setup = true;

    return Value;
  }
}
