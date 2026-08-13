using Core.Models;
using System;

public class RsiIndicator
{
  public int Period { get; set; } = 14;

  // Number of changes processed
  protected int changeCount;
  // Sum of gains/losses for initial SMA
  protected double sumGain;
  protected double sumLoss;
  // Current forming avgs
  protected double avgGain;
  protected double avgLoss;
  // Avgs of last closed bar - seed for Wilder
  protected double prevAvgGain;
  protected double prevAvgLoss;
  protected double? prevClose;
  // Forming bar state for replace
  protected double currentClose;
  protected double currentGain;
  protected double currentLoss;
  protected long? currentTime;
  protected bool setup;

  /// <summary>
  /// Update
  /// </summary>
  /// <param name="stamp"></param>
  /// <param name="price"></param>
  public virtual double Update(long stamp, Price price)
  {
    var close = price.Last.Value;

    // First bar - no change yet

    if (setup is false)
    {
      currentTime = stamp;
      currentClose = close;
      setup = true;

      return 0;
    }

    // Same bar -> replace
    if (currentTime == stamp)
    {
      var change = prevClose == null ? 0 : close - prevClose.Value;
      var gain = Math.Max(change, 0);
      var loss = Math.Max(-change, 0);

      // Replace logic
      if (changeCount <= Period)
      {
        // Initial SMA phase: sum = sum - old + new
        sumGain += gain - currentGain;
        sumLoss += loss - currentLoss;
        avgGain = changeCount == 0 ? 0 : sumGain / changeCount;
        avgLoss = changeCount == 0 ? 0 : sumLoss / changeCount;
      }
      else
      {
        // Wilder phase: (prevClosed * (n-1) + gain) / n
        avgGain = (prevAvgGain * (Period - 1) + gain) / Period;
        avgLoss = (prevAvgLoss * (Period - 1) + loss) / Period;
      }

      // Update forming values
      currentGain = gain;
      currentLoss = loss;
      currentClose = close;

      // RSI formula
      return CalcRsi(avgGain, avgLoss);
    }

    // New bar -> freeze previous forming bar as closed
    // Its close becomes prevClose for next change
    prevClose = currentClose;

    // Freeze its avgs as prevClosed if we finished seed
    if (changeCount >= Period)
    {
      // Seed for Wilder
      prevAvgGain = avgGain;
      prevAvgLoss = avgLoss;
    }

    // Now compute change for new bar
    var newChange = close - prevClose.Value;
    var newGain = Math.Max(newChange, 0);
    var newLoss = Math.Max(-newChange, 0);

    // New change
    changeCount++;

    // Update avgs
    if (changeCount <= Period)
    {
      // Accumulate for SMA seed
      sumGain += newGain;
      sumLoss += newLoss;
      avgGain = sumGain / changeCount;
      avgLoss = sumLoss / changeCount;
    }
    else
    {
      // Wilder smoothing
      avgGain = (prevAvgGain * (Period - 1) + newGain) / Period;
      avgLoss = (prevAvgLoss * (Period - 1) + newLoss) / Period;
    }

    // Store new forming bar
    currentTime = stamp;
    currentClose = close;
    currentGain = newGain;
    currentLoss = newLoss;

    return CalcRsi(avgGain, avgLoss);
  }

  // RSI calc with edge cases
  protected static double CalcRsi(double avgGain, double avgLoss)
  {
    // No loss = 100, no gain / loss = 50

    if (avgLoss is 0)
    {
      return avgGain is 0 ? 50.0 : 100.0;
    }

    // 100 - 100 / (1 + RS)

    var rs = avgGain / avgLoss;

    return 100.0 - 100.0 / (1.0 + rs);
  }
}
