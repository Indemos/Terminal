using System;

namespace Core.Indicators
{
  /// <summary>
  /// Time-Shifted Hayashi-Yoshida (Cross-HY) Indicator.
  /// Evaluates asynchronous cross-correlation across a grid of physical time shifts 
  /// to determine true lead-lag without liquidity/tick-frequency bias.
  /// </summary>
  public class HyIndicator
  {
    protected struct Interval
    {
      public long Min;
      public long Max;
      public double Value;
    }

    protected Interval[] groupX;
    protected Interval[] groupY;
    protected int minX, maxX, countX;
    protected int minY, maxY, countY;

    protected bool setupX, setupY;
    protected long stampX, stampY;
    protected double priceX, priceY;

    /// <summary>Rolling lookback duration (in timestamp units, e.g. TimeSpan.FromSeconds(60).Ticks).</summary>
    public long Frame { get; set; }

    /// <summary>Maximum time offset to check in either direction (e.g. TimeSpan.FromMilliseconds(500).Ticks).</summary>
    public long MaxLag { get; set; }

    /// <summary>Grid resolution step size (e.g. TimeSpan.FromMilliseconds(25).Ticks).</summary>
    public long Step { get; set; }

    /// <summary>The physical time shift where peak correlation occurs (Series X lead/lag in timestamp units).</summary>
    public long OptimalLag { get; protected set; }

    /// <summary>Peak correlation value achieved at OptimalLag.</summary>
    public double MaxCorrelation { get; protected set; }

    /// <summary>Unshifted contemporaneous correlation (Delta t = 0).</summary>
    public double CurrentCorrelation { get; protected set; }

    public HyIndicator(long timeWindow, long maxLag, long step, int capacity = 1024)
    {
      MaxLag = maxLag;
      Frame = timeWindow;
      Step = Math.Max(1, step);

      groupX = new Interval[capacity];
      groupY = new Interval[capacity];
    }

    public void UpdateX(long stamp, double price)
    {
      if (setupX && stamp > stampX)
      {
        Enqueue(ref groupX, ref minX, ref maxX, ref countX, stampX, stamp, Math.Log(price / priceX));
      }

      stampX = stamp;
      priceX = price;
      setupX = true;

      Trim(Math.Max(stampX, stampY));
      Compute();
    }

    public void UpdateY(long stamp, double price)
    {
      if (setupY && stamp > stampY)
      {
        Enqueue(ref groupY, ref minY, ref maxY, ref countY, stampY, stamp, Math.Log(price / priceY));
      }

      stampY = stamp;
      priceY = price;
      setupY = true;

      Trim(Math.Max(stampX, stampY));
      Compute();
    }

    protected void Trim(long stamp)
    {
      var mark = stamp - Frame;

      while (countX > 0 && groupX[maxX].Max <= mark)
      {
        maxX = (maxX + 1) % groupX.Length;
        countX--;
      }

      while (countY > 0 && groupY[maxY].Max <= mark)
      {
        maxY = (maxY + 1) % groupY.Length;
        countY--;
      }
    }

    protected virtual void Compute()
    {
      // 1. Calculate Realized Variances ONCE (invariant under timestamp shifts)
      double varX = 0.0, varY = 0.0;

      for (var i = 0; i < countX; i++)
      {
        var o = groupX[(maxX + i) % groupX.Length].Value;
        varX += o * o;
      }

      for (var i = 0; i < countY; i++)
      {
        var o = groupY[(maxY + i) % groupY.Length].Value;
        varY += o * o;
      }

      var divider = Math.Sqrt(varX * varY);

      if (divider <= 0)
      {
        OptimalLag = 0;
        MaxCorrelation = 0.0;
        CurrentCorrelation = 0.0;
        return;
      }

      var bestCorr = 0.0;
      var bestLag = 0L;

      // 2. Iterate across the time-lag grid
      for (var step = -MaxLag; step <= MaxLag; step += Step)
      {
        var covXY = 0.0;
        var ii = 0;
        var i = 0;

        // O(N + M) Two-Pointer traversal with shifted X intervals
        while (i < countX && ii < countY)
        {
          var ix = groupX[(maxX + i) % groupX.Length];
          var iy = groupY[(maxY + ii) % groupY.Length];

          // Shift X's timeline by 'shift'
          var min = ix.Min + step;
          var max = ix.Max + step;

          // Overlap test: (Shifted X starts before Y ends) AND (Y starts before Shifted X ends)
          if (min < iy.Max && iy.Min < max)
          {
            covXY += ix.Value * iy.Value;
          }

          // Advance pointer for whichever interval ends earlier in physical time
          _ = (max < iy.Max) ? i++ : ii++;
        }

        var corr = covXY / divider;

        if (step == 0)
        {
          CurrentCorrelation = corr;
        }

        if (Math.Abs(corr) > Math.Abs(bestCorr))
        {
          bestCorr = corr;
          bestLag = step;
        }
      }

      OptimalLag = bestLag;
      MaxCorrelation = bestCorr;
    }

    protected virtual void Enqueue(ref Interval[] group, ref int min, ref int max, ref int count, long start, long end, double value)
    {
      if (count == group.Length)
      {
        var newBuf = new Interval[group.Length * 2];

        for (int i = 0; i < count; i++)
        {
          newBuf[i] = group[(max + i) % group.Length];
        }

        group = newBuf;
        min = count;
        max = 0;
      }

      group[min] = new() { Min = start, Max = end, Value = value };
      min = (min + 1) % group.Length;
      count++;
    }
  }
}
