using Core.Conventions;
using System;

namespace Core.Indicators
{
  /// <summary>
  /// Calculates running statistics (Mean and Standard Deviation) 
  /// using Welford's Algorithm for numerical stability.
  /// </summary>
  public class StatIndicator : Indicator
  {
    private int count;
    private double mean;
    private double summary;

    /// <summary>
    /// Returns the Sample Standard Deviation.
    /// Returns 0.0 if fewer than 2 data points have been added.
    /// </summary>
    public virtual double Deviation => count > 1 ? Math.Sqrt(summary / (count - 1)) : 0;

    /// <summary>
    /// Returns the Running Mean.
    /// </summary>
    public virtual double Mean => mean;

    /// <summary>
    /// Returns the number of samples processed.
    /// </summary>
    public virtual int Count => count;

    /// <summary>
    /// Returns the Sample Variance.
    /// </summary>
    public virtual double Variance => count > 1 ? summary / (count - 1) : 0;

    /// <summary>
    /// Calculate
    /// </summary>
    /// <param name="value"></param>
    public virtual IIndicator Update(double value)
    {
      count++;

      // Use the difference from the current mean
      double currentAverage = value - mean;

      // Update the running mean
      mean += currentAverage / count;

      // Use the difference from the newly updated mean
      double nextAverage = value - mean;

      // Accumulate the squared differences from the mean
      summary += currentAverage * nextAverage;

      return this;
    }
  }
}
