using Core.Models;

namespace Core.Indicators
{
  public class EmaIndicator
  {
    /// <summary>
    /// Period for EMA calculation
    /// </summary>
    public int Period { get; set; } = 15;

    // alpha = 2 / (n+1)
    protected double weight => 2.0 / (Period + 1);
    // Count of bars including forming
    protected int count;
    // Sum of first Period for SMA seed
    protected double sum;
    // Current forming EMA
    protected double ema;
    // EMA of last closed bar - seed for Wilder
    protected double previousEma;
    // Time of forming bar
    protected long? currentTime;
    // Value of forming bar to replace
    protected double currentValue;
    // First bar seen
    protected bool setup;

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
          ema = sum / count;
        }
        else
        {
          // EMA phase: EMA = alpha*price + (1-alpha)*prevClosed
          ema = weight * price + (1 - weight) * previousEma;
        }

        // Update forming value
        currentValue = price;

        return ema;
      }

      // New bar -> previous forming bar is now closed
      if (setup)
      {
        // Freeze its EMA as previous closed EMA if we finished seed
        if (count >= Period)
        {
          // This becomes the seed for next bar's EMA
          previousEma = ema;
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
        ema = sum / count;
      }
      else
      {
        // Wilder EMA
        ema = weight * price + (1 - weight) * previousEma;
      }

      // Store forming bar info
      currentTime = stamp;
      currentValue = price;
      setup = true;

      return ema;
    }
  }
}
