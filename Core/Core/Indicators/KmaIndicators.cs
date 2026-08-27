namespace Core.Indicators
{
  /// <summary>
  /// Kalman Moving Average Indicator
  /// </summary>
  public class KmaIndicator
  {
    protected bool setup;
    protected double variance;

    /// <summary>
    /// Process noise to control the smoothness of the moving average.
    /// Smaller value results in a smoother average.
    /// Larger value allows for more responsiveness to changes in the input data.
    /// </summary>
    public virtual double ProcessNoise { get; set; } = 0.00001;

    /// <summary>
    /// Observation noise to account for measurement errors or fluctuations in the input data.
    /// Smaller value assumes more confidence in the observed data.
    /// Larger value allows for more uncertainty.
    /// </summary>
    public virtual double ObservationNoise { get; set; } = 0.01;

    /// <summary>
    /// The current mean value of the Kalman Moving Average.
    /// </summary>
    public virtual double Mean { get; protected set; }

    /// <summary>
    /// Calculate
    /// </summary>
    /// <param name="price"></param>
    public virtual double Update(double price)
    {
      if (setup is false)
      {
        variance = 1.0;
        setup = true;

        return Mean = price;
      }

      // Predict
      variance += ProcessNoise;

      // Kalman gain
      var innovationVariance = variance + ObservationNoise;
      var gain = innovationVariance is 0 ? Mean : (variance / innovationVariance);

      // Update
      Mean += gain * (price - Mean);

      // Update variance
      variance *= (1.0 - gain);

      return Mean;
    }
  }
}
