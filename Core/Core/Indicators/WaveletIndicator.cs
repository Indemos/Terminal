using System;
using System.Numerics;

namespace Core.Indicators
{
  /// <summary>
  /// Haar Wavelet Moving Average / Denoising Indicator.
  ///
  /// Decomposes a rolling price window into multiple Haar wavelet
  /// scales, suppresses high-frequency detail coefficients and
  /// reconstructs a smoothed price.
  /// </summary>
  public class WaveletIndicator
  {
    // Circular price history and ring-buffer state.
    protected readonly double[] observations;
    protected int count;
    protected int index;

    /// <summary>
    /// Number of observations used for the wavelet decomposition.
    /// Must be a power of two.
    /// </summary>
    public virtual int Period { get; }

    /// <summary>
    /// Number of decomposition levels.
    /// Period = 16:
    /// Level 1 = 2 observations
    /// Level 2 = 4 observations
    /// Level 3 = 8 observations
    /// Level 4 = 16 observations
    /// </summary>
    public virtual int Levels { get; }

    /// <summary>
    /// Detail threshold.
    /// Detail coefficients whose absolute value is below this
    /// threshold are removed.
    /// Zero means no thresholding.
    /// </summary>
    public virtual double Threshold { get; set; }

    /// <summary> Wavelet-smoothed price. </summary>
    public virtual double Mean { get; protected set; }

    /// <summary> Fastest wavelet component. </summary>
    public virtual double Detail1 { get; protected set; }

    /// <summary> Second wavelet component. </summary>
    public virtual double Detail2 { get; protected set; }

    /// <summary> Third wavelet component. </summary>
    public virtual double Detail3 { get; protected set; }

    /// <summary> Total wavelet detail energy. </summary>
    public virtual double Energy { get; protected set; }

    public WaveletIndicator(int period = 16, int levels = 3)
    {
      if (period < 2 || (period & (period - 1)) is not 0)
      {
        throw new ArgumentException("Period must be a power of two.", nameof(period));
      }

      Period = period;
      Levels = levels;
      observations = new double[period];
    }

    /// <summary>
    /// Add a new price and calculate the wavelet-smoothed price.
    /// </summary>
    public virtual double Update(double price)
    {
      // Ring-buffer write with wrap-around.
      observations[index] = price;
      index = (index + 1) % Period;

      // Warm-up period - return raw price.
      if (++count < Period)
      {
        return Mean = price;
      }

      return Transform();
    }

    /// <summary>
    /// Perform a forward Haar DWT, optionally remove small detail
    /// coefficients, then perform the inverse DWT to reconstruct
    /// the smoothed price.
    /// </summary>
    protected virtual double Transform()
    {
      // Haar's orthonormal transform uses 1 / sqrt(2).
      // Without this normalization, repeated transformations would change the total signal energy.
      var sqrtTwo = Math.Sqrt(2.0);

      // Working array for the DWT.
      // The original observations remain in the circular buffer. "work" is transformed in-place through successive levels.
      // Only the current approximation remains in "work".
      // prices => approximation 1 => approximation 2 => approximation 3
      var work = new double[Period];

      // Offset the circular buffer to chronological order.
      for (var i = 0; i < Period; i++)
      {
        work[i] = observations[(index + i) % Period];
      }

      // Each element contains all detail coefficients produced at one particular wavelet scale.
      // details[0] = fastest detail scale
      // details[1] = next / coarser scale
      // details[2] = next / coarser scale
      var period = Period;
      var details = new double[Levels][];

      // Forward Haar DWT.
      // Iteratively split into averages (approximation) and differences (detail).
      // A = (a + b) / sqrt(2) -> approximation
      // D = (a - b) / sqrt(2) -> detail
      // A is the local average / low-frequency component.
      // D is the local difference / high-frequency component.
      // We then discard the detail from the input to the next level and decompose only the approximation again.
      for (var i = 0; i < Levels; i++)
      {
        var size = period >> 1;
        var detail = new double[size];
        var approximation = new double[size];

        // Each pair of input values produces one approximation and one detail coefficient, so the number of coefficients
        // is reduced by half at every level.
        // Period = 16:
        // Level 1: 16 -> 8 approximation + 8 detail
        // Level 2:  8 -> 4 approximation + 4 detail
        // Level 3:  4 -> 2 approximation + 2 detail
        for (var pairIdx = 0; pairIdx < size; pairIdx++)
        {
          // Select two adjacent values from the current scale.
          // At level 1 these are actual prices:
          // work[0], work[1]
          // work[2], work[3]
          // At later levels they are approximation coefficients produced by the previous level.
          var current = work[pairIdx * 2];
          var next = work[pairIdx * 2 + 1];

          // Haar approximation coefficient.
          // A = (a + b) / sqrt2 
          // This combines two neighboring values and therefore
          // removes their local difference.
          // Large A -> relatively large local price level.
          approximation[pairIdx] = (current + next) / sqrtTwo;

          // Haar detail coefficient.
          // D = (a - b) / sqrt2 
          // This measures the difference between the two values.
          // Positive D -> current > next
          // Negative D -> current < next
          // Near zero -> the two values are similar.
          // This is the component that captures short-term variation at the current wavelet scale.
          detail[pairIdx] = (current - next) / sqrtTwo;
        }

        // Save the detail coefficients for this scale.
        // These are needed later for:
        // 1. thresholding / denoising
        // 2. calculating energy
        // 3. inverse reconstruction
        details[i] = detail;
        period = size;

        Array.Copy(approximation, work, size);
      }

      // Energy = sum(detail^2) = measure of high-frequency noise / volatility.
      // At this point:
      // - work = coarsest approximation
      // - details = detail coefficients from every scale
      // The detail coefficients contain local price changes.
      // Thresholding removes or reduces those small changes before the inverse transform reconstructs the signal.
      // D' = sign(D) * max(|D| - Threshold, 0)
      // |D| <= Threshold -> D' = 0
      // |D| >  Threshold -> D' moves toward zero
      // Energy: E = sum(D'^2)
      // Measures the amount of variation remaining in the detail components after thresholding.
      Energy = 0;

      for (var i = 0; i < Levels; i++)
      {
        var group = details[i];

        for (var pos = 0; pos < group.Length; pos++)
        {
          if (Threshold > 0)
          {
            var detail = group[pos];
            var magnitude = Math.Abs(detail) - Threshold;

            group[pos] = magnitude > 0 ? Math.Sign(detail) * magnitude : 0;
          }

          Energy += group[pos] * group[pos];
        }
      }

      // Save the latest coefficient from the first few scales.
      // Detail1 = finest / fastest wavelet scale
      // Detail2 = next / slower scale
      // Detail3 = next / even slower scale
      // The last coefficient is selected because it corresponds to the most recent part of the chronological window.
      Detail1 = details.Length > 0 ? details[0][^1] : 0;
      Detail2 = details.Length > 1 ? details[1][^1] : 0;
      Detail3 = details.Length > 2 ? details[2][^1] : 0;

      // Inverse transform.
      // "work" currently contains the coarsest approximation.
      // Reconstruct all levels in reverse order.
      for (var i = Levels - 1; i >= 0; i--)
      {
        // Number of approximation coefficients at this level.
        var size = Period >> (i + 1);
        var group = new double[size * 2];

        for (var pairIdx = 0; pairIdx < size; pairIdx++)
        {
          var approximation = work[pairIdx];
          var detail = details[i][pairIdx];

          // Inverse Haar transform
          // x0 = (A + D) / sqrt(2)
          // x1 = (A - D) / sqrt(2)
          // is the inverse of original statement
          // A = (x0 + x1) / sqrt(2)
          // D = (x0 - x1) / sqrt(2)
          group[pairIdx * 2] = (approximation + detail) / sqrtTwo;
          group[pairIdx * 2 + 1] = (approximation - detail) / sqrtTwo;
        }

        Array.Copy(group, work, group.Length);
      }

      // After the inverse transform, "work" contains the complete reconstructed rolling price window.
      // Because the window was arranged chronologically before the forward transform
      // returning the newest reconstructed value gives us wavelet-smoothed version of the current price.
      return Mean = work[Period - 1];
    }
  }
}
