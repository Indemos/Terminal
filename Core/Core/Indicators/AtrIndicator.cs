using Core.Models; 
using System; 

public class AtrIndicator
{
  // Count of TRs seen including current forming bar
  protected int rangeCount;
  // Sum of first Period TRs to build initial SMA
  protected double sum;
  // ATR of last fully closed bar, used as seed for Wilder
  protected double previousAtr;
  // Close of last fully closed bar, used for TrueRange
  protected double? previousClose;

  // Timestamp of the bar we are currently forming
  protected long currentTime;
  // TR of current forming bar so we can replace it on same-bar update
  protected double currentTr;
  // Close of current forming bar, becomes previousClose when bar closes
  protected double currentClose;
  // True after first bar has been processed
  protected bool setup;

  /// <summary>
  /// Number of bars for Wilder smoothing
  /// </summary>
  public virtual int Period { get; set; } = 15;

  // Current value
  public virtual double Value { get; protected set; }

  /// <summary>
  /// Called every tick, stamp is bar time
  /// </summary>
  /// <param name="stamp"></param>
  /// <param name="price"></param>
  public virtual double Update(long stamp, Price price)
  {
    if (price.Bar is null ||
        price.Bar.Low is not double L ||
        price.Bar.High is not double H ||
        price.Bar.Close is not double C)
    {
      return Value;
    }

    // Check if we already have a forming bar
    if (setup)
    {
      // Same timestamp means intrabar update of same bar
      if (currentTime == stamp)
      {
        return Value = UpdateRange(H, L, C, isReplace: true);
      }

      // Final close of forming bar becomes previous close for next TR
      previousClose = currentClose;

      // If we have at least Period bars, freeze its ATR as previous closed ATR
      if (rangeCount >= Period)
      {
        // This is the seed for Wilder smoothing on next bar
        previousAtr = Value;
      }
    }

    // New bar path
    // Compute TR for new bar and update count/sum/ATR
    Value = UpdateRange(H, L, C, isReplace: false);
    // Store time of this new forming bar
    currentTime = stamp;
    // Mark that we have a forming bar now
    setup = true;

    return Value;
  }

  /// <summary>
  // Computes TrueRange and applies SMA / Wilder logic
  /// </summary>
  /// <param name="H"></param>
  /// <param name="L"></param>
  /// <param name="C"></param>
  /// <param name="isReplace"></param>
  protected double UpdateRange(double H, double L, double C, bool isReplace)
  {
    var response = 0.0;
    var tr = previousClose is null ? H - L : Math.Max(H - L, Math.Max(
      Math.Abs(H - previousClose.Value),
      Math.Abs(L - previousClose.Value)));

    // New bar increments count, replace keeps count same
    rangeCount += isReplace ? 0 : 1;

    // Initial phase: build SMA
    if (rangeCount <= Period)
    {
      // New bar: add TR to sum. Replace: remove old TR and add new TR
      sum += tr - (isReplace ? currentTr : 0);
      // ATR is simple average so far
      response = sum / rangeCount;
    }
    else
    {
      // Wilder formula: (prevClosed * (n - 1) + TR) / n
      response = (previousAtr * (Period - 1) + tr) / Period;
    }

    currentTr = tr;
    currentClose = C;

    return response;
  }
}
