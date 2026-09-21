using System;

namespace Core.Indicators
{
  /// <summary>
  /// Rolling Hayashi-Yoshida covariance/correlation estimator for two asynchronous return processes.
  /// Each observation represents a return measured over a physical time interval [Min, Max]:
  /// r_i = log(P_i / P_{i-1})
  ///
  /// For asynchronous observations, ordinary Pearson correlation is not appropriate because the observations do not occur at identical times.
  /// The Hayashi-Yoshida estimator estimates covariance by summing products of returns whose observation intervals overlap:
  ///
  /// HY(X,Y) = Σ rX_i * rY_j * I(interval X_i overlaps interval Y_j)
  ///
  /// For a time shift τ, X intervals are shifted by τ:
  ///
  /// [X.Min + τ, X.Max + τ]
  ///
  /// and the same overlap rule is applied.
  ///
  /// The resulting correlation is:
  ///
  /// ρ(τ) = HY(X,Y;τ) / sqrt(RV_X * RV_Y)
  ///
  /// where:
  ///
  /// RV_X = Σ rX_i²
  /// RV_Y = Σ rY_j²
  ///
  /// The realized variances do not depend on τ, so they are calculated once
  /// for the current rolling window.
  /// </summary>
  public class LeadingIndicator
  {
    protected struct Interval
    {
      /// <summary>Physical start timestamp of the return interval.</summary>
      public long Min;

      /// <summary>Physical end timestamp of the return interval.</summary>
      public long Max;

      /// <summary>Log return observed over [Min, Max].</summary>
      public double Value;
    }

    protected Interval[] groupX;
    protected Interval[] groupY;

    // Write positions in the circular buffers.
    protected int maxX;
    protected int maxY;

    // Read positions: oldest retained interval.
    protected int minX;
    protected int minY;

    protected int countX;
    protected int countY;

    /// <summary>Rolling physical-time window.</summary>
    public long Frame { get; }

    /// <summary>Number of X intervals currently retained.</summary>
    public int CountX => countX;

    /// <summary>Number of Y intervals currently retained.</summary>
    public int CountY => countY;

    /// <summary>
    /// True when both series contain at least one return interval.
    /// A stronger readiness condition can be imposed by the caller.
    /// </summary>
    public bool IsReady => countX > 0 && countY > 0;

    public LeadingIndicator(long frame, int capacity = 1024)
    {
      Frame = frame;

      groupX = new Interval[capacity];
      groupY = new Interval[capacity];
    }

    /// <summary>
    /// Adds one completed X return interval.
    ///
    /// The caller supplies the physical interval over which the return was
    /// measured and the return itself.
    /// </summary>
    public virtual void UpdateX(long start, long end, double value)
    {
      if (end <= start)
      {
        return;
      }

      Enqueue(ref groupX, ref maxX, ref minX, ref countX, start, end, value);
    }

    /// <summary>
    /// Adds one completed Y return interval.
    /// </summary>
    public virtual void UpdateY(long start, long end, double value)
    {
      if (end <= start)
      {
        return;
      }

      Enqueue(ref groupY, ref maxY, ref minY, ref countY, start, end, value);
    }

    /// <summary>
    /// Removes intervals that are completely outside the rolling window.
    ///
    /// The window is defined relative to the supplied physical timestamp:
    ///
    /// [timestamp - Frame, timestamp]
    ///
    /// An interval is removed when its end is at or before the window start.
    /// </summary>
    public virtual void Trim(long timestamp)
    {
      var mark = timestamp - Frame;

      while (countX > 0 && groupX[minX].Max <= mark)
      {
        minX++;
        if (minX == groupX.Length) minX = 0;
        countX--;
      }

      while (countY > 0 && groupY[minY].Max <= mark)
      {
        minY++;
        if (minY == groupY.Length) minY = 0;
        countY--;
      }
    }

    /// <summary>
    /// Calculates the Hayashi-Yoshida covariance for a specified physical
    /// time shift.
    /// Positive shift moves X forward in time:
    /// X'[t] = X[t - shift]
    /// Therefore, a positive shift means that X returns occurred earlier
    /// than the corresponding Y returns — i.e. X leads Y.
    /// HY covariance:
    /// Cov_HY(τ) = Σ X_i * Y_j
    /// for every pair whose shifted physical intervals overlap.
    /// Returns null when there is insufficient data.
    /// </summary>
    public virtual double? Covariance(long shift)
    {
      if (IsReady is false)
      {
        return null;
      }

      var covariance = 0.0;

      var ix = 0;
      var iy = 0;

      while (ix < countX && iy < countY)
      {
        var x = groupX[(minX + ix) % groupX.Length];
        var y = groupY[(minY + iy) % groupY.Length];

        // Shift the X observation interval by τ.
        var xMin = x.Min + shift;
        var xMax = x.Max + shift;

        // Hayashi-Yoshida covariance includes the product of two returns
        // whenever their observation intervals overlap:
        // xMin < yMax && yMin < xMax
        if (xMin < y.Max && y.Min < xMax)
        {
          covariance += x.Value * y.Value;
        }

        // Move past whichever interval ends first.
        // If both end at exactly the same time, both can be advanced because
        // neither can overlap another interval after its endpoint.
        (ix, iy) = (xMax.CompareTo(y.Max)) switch
        {
          < 0 => (ix + 1, iy),
          > 0 => (ix, iy + 1),
          _ => (ix + 1, iy + 1)
        };
      }

      return covariance;
    }

    /// <summary>
    /// Calculates realized variance of X:
    /// RV_X = Σ X_i²
    /// This quantity is invariant to the time shift applied to X.
    /// </summary>
    public virtual double VarianceX()
    {
      var variance = 0.0;

      for (var i = 0; i < countX; i++)
      {
        var value = groupX[(minX + i) % groupX.Length].Value;
        variance += value * value;
      }

      return variance;
    }

    /// <summary>
    /// Calculates realized variance of Y:
    ///
    /// RV_Y = Σ Y_i²
    /// </summary>
    public virtual double VarianceY()
    {
      var variance = 0.0;

      for (var i = 0; i < countY; i++)
      {
        var value = groupY[(minY + i) % groupY.Length].Value;
        variance += value * value;
      }

      return variance;
    }

    /// <summary>
    /// Calculates normalized Hayashi-Yoshida correlation:
    ///
    /// ρ(τ) = HY(X,Y;τ) / sqrt(RV_X * RV_Y)
    ///
    /// Returns null when there is insufficient data or either realized
    /// variance is zero.
    /// </summary>
    public virtual double? Correlation(long position)
    {
      if (IsReady is false)
      {
        return null;
      }

      var varianceX = VarianceX();
      var varianceY = VarianceY();
      var denominator = Math.Sqrt(varianceX * varianceY);

      return denominator <= 0.0 ? null : Covariance(position).Value / denominator;
    }

    /// <summary>
    /// Adds an interval to a circular buffer.
    ///
    /// The buffer grows automatically when capacity is exhausted.
    /// Growth is rare; normal Update() calls do not allocate.
    /// </summary>
    protected virtual void Enqueue(
      ref Interval[] group,
      ref int min,
      ref int max,
      ref int count,
      long start,
      long end,
      double value)
    {
      if (count == group.Length)
      {
        var copy = new Interval[group.Length * 2];

        for (var i = 0; i < count; i++)
        {
          copy[i] = group[(max + i) % group.Length];
        }

        group = copy;

        max = 0;
        min = count;
      }

      group[min] = new Interval
      {
        Min = start,
        Max = end,
        Value = value
      };

      min++;

      if (min == group.Length)
      {
        min = 0;
      }

      count++;
    }
  }
}
