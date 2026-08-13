using System;

namespace Core.Indicators
{
  /// <summary>
  /// Calculates running statistics (Mean and Standard Deviation) 
  /// using Welford's Algorithm for numerical stability.
  /// </summary>
  public class VarianceIndicator
  {
    protected int count;
    protected double mean;
    protected double summary;

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
    /// Returns the Sample Variance.
    /// </summary>
    public virtual double Variance => count > 1 ? summary / (count - 1) : 0;

    /// <summary>
    /// Calculate
    /// </summary>
    /// <param name="value"></param>
    public virtual VarianceIndicator Update(double value)
    {
      count++;

      // Use the difference from the current mean
      var currentAverage = value - mean;

      // Update the running mean
      mean += currentAverage / count;

      // Use the difference from the newly updated mean
      var nextAverage = value - mean;

      // Accumulate the squared differences from the mean
      summary += currentAverage * nextAverage;

      return this;
    }
  }
}
