using System;
using System.Collections.Generic;

namespace Core.Indicators
{
  /// <summary>
  /// Hayashi-Yoshida estimator for measuring the relationship between two asynchronously sampled time series.
  /// Each price observation is converted into a return interval:
  /// [previous timestamp, current timestamp]
  /// The HY covariance is then calculated by multiplying returns whose time intervals overlap.
  /// This allows X and Y to be sampled at different times without resampling either series onto a common time grid.
  /// </summary>
  public class HyIndicator(long timeWindow)
  {
    /// <summary>
    /// Represents the return accumulated between two consecutive observations of one series.
    /// Example:
    /// Price at 10:00:00 = 100
    /// Price at 10:00:05 = 101
    /// Interval:
    /// Min = 10:00:00
    /// Max = 10:00:05
    /// Value = return over that interval
    /// The interval is treated as (Min, Max] for overlap purposes.
    /// </summary>
    protected struct Interval
    {
      public long Min;
      public long Max;
      public double Value;
    }

    // Intervals are stored chronologically.
    // X and Y do not need to have matching timestamps.
    // For example:
    // X: [0,100], [100,200], [200,300]
    // Y: [0,70],  [70,180],  [180,300]
    // HY determines which of these intervals overlap.
    protected readonly Queue<Interval> itemsX = new();
    protected readonly Queue<Interval> itemsY = new();

    // Setup
    protected bool setupX;
    protected bool setupY;

    // Timestamp of the most recent observation for each series.
    protected long stampX;
    protected long stampY;

    // Price corresponding to the most recent timestamp.
    protected double priceX;
    protected double priceY;

    /// <summary>
    /// If true, interval values are logarithmic returns: log(Pt / Pt-1)
    /// If false, interval values are simple returns: (Pt - Pt-1) / Pt-1
    /// For financial time series, log returns are generally preferable because they are additive across consecutive intervals.
    /// </summary>
    public virtual bool UseReturns { get; set; }

    /// <summary>
    /// Time window used for the rolling HY calculation.
    /// Must use the SAME units as the timestamps.
    /// For example, if timestamps are Unix milliseconds: 60 seconds = 60,000
    /// Each series is trimmed independently using its own latest timestamp.
    /// </summary>
    public long Frame { get; set; } = timeWindow;

    /// <summary>
    /// Average difference between the start times of overlapping X / Y intervals.
    /// Positive value: Y intervals generally start later than X intervals. This suggests X is leading Y.
    /// Negative value: X intervals generally start later than Y intervals. This suggests Y is leading X.
    /// This is only a simple timing heuristic. It is NOT part of the formal Hayashi-Yoshida estimator.
    /// </summary>
    public virtual double LeadBias { get; protected set; }

    /// <summary>
    /// Variance is used to normalize HY covariance into correlation.
    /// </summary>
    public virtual double VarianceX { get; protected set; }
    public virtual double VarianceY { get; protected set; }

    /// <summary>
    /// Hayashi-Yoshida covariance: Σ Xi * Yj
    /// For every pair of intervals that overlap in time.
    /// </summary>
    public virtual double Covariance { get; protected set; }

    /// <summary>
    /// HY correlation: Covariance / sqrt(VarianceX * VarianceY)
    /// Positive values indicate same-direction movement.
    /// Negative values indicate opposite-direction movement.
    /// </summary>
    public virtual double Correlation { get; protected set; }

    /// <summary>
    /// Adds a new observation to series X.
    /// </summary>
    public void UpdateX(long timestamp, double price)
    {
      // Once a previous observation exists, the new observation closes an interval.
      // The return represents the price movement accumulated during that entire interval.
      if (setupX && timestamp > stampX)
      {
        itemsX.Enqueue(new()
        {
          Min = stampX,
          Max = timestamp,
          Value = UseReturns ? Math.Log(price / priceX) : ((price - priceX) / priceX)
        });
      }

      stampX = timestamp;
      priceX = price;
      setupX = true;

      // Remove intervals outside the rolling time window.
      // X is trimmed using X's own latest timestamp because X and Y can arrive asynchronously.
      TrimQueue(itemsX, stampX);

      // Recalculate the HY statistics using the current X / Y windows.
      Compute();
    }

    /// <summary>
    /// Adds a new observation to series Y.
    /// </summary>
    public void UpdateY(long timestamp, double price)
    {
      if (setupY && timestamp > stampY)
      {
        itemsY.Enqueue(new()
        {
          Min = stampY,
          Max = timestamp,
          Value = UseReturns ? Math.Log(price / priceY) : ((price - priceY) / priceY)
        });
      }

      stampY = timestamp;
      priceY = price;
      setupY = true;

      TrimQueue(itemsY, stampY);
      Compute();
    }

    /// <summary>
    /// Removes intervals that are completely outside the rolling time window.
    /// Because intervals are stored chronologically, the oldest interval is always at the front of the queue.
    /// Therefore we can remove expired intervals in O(number of removed intervals).
    /// </summary>
    protected virtual void TrimQueue(Queue<Interval> queue, long stamp)
    {
      // Anything ending at or before this timestamp is completely outside the desired rolling window.
      var mark = stamp - Frame;

      // Remove old intervals from the front.
      // We only remove an interval when its END is before the cutoff.
      // An interval that started before the cutoff but ends after it still overlaps the active window and therefore must remain.
      while (queue.Count > 0 && queue.Peek().Max <= mark)
      {
        queue.Dequeue();
      }
    }

    /// <summary>
    /// Calculates HY covariance, realized variances, correlation, and the simple start-time lead bias.
    /// </summary>
    protected virtual void Compute()
    {
      // There is nothing meaningful to calculate until both series contain enough return intervals.
      if (itemsX.Count < 2 || itemsY.Count < 2)
      {
        Covariance = Correlation = LeadBias = VarianceX = VarianceY = 0;
        return;
      }

      var sum = 0.0;
      var count = 0;
      var varX = 0.0;
      var varY = 0.0;
      var covXY = 0.0;

      // Queue<T> does not provide indexed access, so convert the queues into chronological arrays for the two-pointer scan.
      var arrX = itemsX.ToArray();
      var arrY = itemsY.ToArray();

      // Realized variance of X: VarX = Σ Xi²
      // HY uses realized quadratic variation rather than the usual sample variance with subtraction of the arithmetic mean.
      foreach (var ix in arrX)
      {
        varX += ix.Value * ix.Value;
      }

      // Realized variance of Y: VarY = Σ Yi²
      foreach (var iy in arrY)
      {
        varY += iy.Value * iy.Value;
      }

      // i = current X interval
      // ii = current Y interval
      // Both arrays are sorted chronologically, so we can walk through
      // them with two pointers instead of comparing every X interval
      // against every Y interval.
      int i = 0, ii = 0;

      while (i < arrX.Length && ii < arrY.Length)
      {
        var ix = arrX[i];
        var iy = arrY[ii];

        // Two intervals overlap when: X.Start < Y.End AND Y.Start < X.End
        // In other words, neither interval has completely finished before the other one begins.
        // X: [0 -------- 100]
        // Y:       [50 -------- 150]
        // They overlap from 50 to 100, so Xi * Yi contributes to the HY covariance.
        if (ix.Min < iy.Max && iy.Min < ix.Max)
        {
          // Core Hayashi-Yoshida covariance contribution.
          // The full return Xi is multiplied by the full return Yi.
          // We do NOT scale the returns by the duration of their overlap. This is a key property of the HY estimator.
          covXY += ix.Value * iy.Value;

          // Timing heuristic.
          // If X starts at 100 and Y starts at 150: Y.Min - X.Min = +50
          // Positive => X started first => X potentially leads Y.
          // If the result is negative, Y started first.
          sum += iy.Min - ix.Min;

          // Count the overlapping interval pairs used by this heuristic.
          count++;
        }

        // Advance whichever interval finishes first.
        // Because the intervals are chronological, the interval that
        // ends first can never overlap any later interval on its own
        // side, so it is safe to discard it from the comparison.
        switch (ix.Max < iy.Max)
        {
          case true: i++; break; // X ends first
          case false: ii++; break; // Y ends first or at the same time
        }
      }

      // Convert HY covariance into a correlation coefficient: Corr = Cov / sqrt(VarX * VarY)
      // The denominator is zero when one of the series has no realized movement.
      var denominator = Math.Sqrt(varX * varY);

      VarianceX = varX;
      VarianceY = varY;
      Covariance = covXY;

      // Average interval-start difference.
      // Positive -> X tends to start overlapping intervals first.
      // Negative -> Y tends to start first.
      LeadBias = count > 0 ? sum / count : 0.0;

      // Normalize covariance into correlation.
      Correlation = denominator > 0 ? covXY / denominator : 0.0;
    }

    /// <summary>
    /// Searches for the time shift that produces the strongest HY relationship between X and Y.
    /// Positive lag: Y is shifted later in time. X leads Y.
    /// Negative lag: Y is shifted earlier in time. Y leads X.
    /// The search evaluates: -maxLagMs ... 0 ... +maxLagMs using the specified step size.
    /// </summary>
    public (long lag, double corr, double cov) EstimateBias(long maxLagMs, long stepMs = 100)
    {
      // At least one interval from each series is required.
      if (itemsX.Count is 0 || itemsY.Count is 0) return (0, 0, 0);

      // The same realized variances are used to normalize every lagged covariance.
      // Therefore: Corr(lag) = Cov(lag) / sqrt(VarX * VarY)
      // We do not need to recalculate the denominator for every lag.
      var denom = Math.Sqrt(VarianceX * VarianceY);

      if (denom <= 0) return (0, 0, 0);

      // Convert queues into chronological arrays.
      var arrX = itemsX.ToArray();
      var arrY = itemsY.ToArray();

      // Start with zero lag as the baseline.
      // This is important because a non-zero lag must actually produce
      // a stronger relationship than the naturally occurring
      // contemporaneous relationship.
      var bestLag = 0L;
      var bestCov = Covariance;
      var bestCorr = Correlation;
      var bestAbs = Math.Abs(Correlation);

      // Test a range of possible timing offsets.
      // maxLagMs = 500
      // stepMs = 100
      // Positive lag means Y is moved later, which corresponds to X potentially leading Y.
      for (var lag = -maxLagMs; lag <= maxLagMs; lag += stepMs)
      {
        var i = 0;
        var ii = 0;
        var cov = 0.0;

        // Find overlapping intervals after shifting all Y intervals by the current lag.
        // This is still an O(N + M) two-pointer scan.
        while (i < arrX.Length && ii < arrY.Length)
        {
          var x = arrX[i];
          var y = arrY[ii];

          // Shift the Y interval in time.
          // X: [------]
          // Y:     [------]
          // This tests whether X's movement is followed by Y's movement.
          // Negative lag moves Y earlier and therefore tests whether Y leads X.
          var yMin = y.Min + lag;
          var yMax = y.Max + lag;

          // If the shifted intervals overlap, their return product contributes to the lagged HY covariance.
          if (x.Min < yMax && yMin < x.Max)
          {
            cov += x.Value * y.Value;
          }

          // Advance the interval that ends first.
          // This preserves the O(N + M) overlap traversal.
          switch (x.Max.CompareTo(yMax))
          {
            case < 0: i++; break; // X ends first
            case > 0: ii++; break; // Y ends first
            default: i++; ii++; break; // Both end at the same time, advance both
          }
        }

        // The variances do not change when we shift Y in time.
        // Therefore the same denominator can be used for every lag.
        var corr = cov / denom;
        var absCorr = Math.Abs(corr);

        // Keep the lag with the strongest absolute relationship.
        // Using absolute correlation means both +0.80 and -0.80 are considered equally strong relationships.
        // The sign is preserved in bestCorr so we can still determine
        // whether the relationship is positive or negative.
        if (absCorr > bestAbs)
        {
          bestAbs = absCorr;
          bestCorr = corr;
          bestCov = cov;
          bestLag = lag;
        }
      }

      // Return:
      // lag -> estimated leader / follower delay
      // corr -> strength and direction of the relationship
      // cov -> corresponding HY covariance
      // Positive lag: X leads Y 
      // Negative lag: Y leads X
      return (bestLag, Math.Clamp(bestCorr, -1, 1), bestCov);
    }
  }
}
