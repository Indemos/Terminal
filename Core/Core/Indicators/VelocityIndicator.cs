namespace Core.Indicators;

/// <summary>
/// Zero-mean KMA indicator with trend-following velocity estimation using a Kalman filter.
/// State vector: x = [Value, Mean]
/// Value = estimated current price level
/// Mean = estimated price velocity / trend
/// The filter assumes:
/// Value(t) = Value(t-1) + Mean(t-1)
/// Mean(t)  = Mean(t-1)
/// Therefore Mean represents the amount the estimated price is expected to move during the next observation period.
/// </summary>
public class VelocityIndicator
{
  /// <summary>
  /// Kalman-filtered price level.
  /// This is the first component of the state vector.
  /// </summary>
  public double Value { get; protected set; }

  /// <summary>
  /// Estimated price velocity / trend.
  /// Despite the name "Mean", this is NOT a conventional moving average.
  /// It is the second state of the Kalman filter and represents the estimated change in price per observation.
  /// It is therefore naturally zero-centered:
  /// > 0  = upward trend
  /// < 0  = downward trend
  /// ~ 0  = no directional movement
  /// </summary>
  public double Mean { get; protected set; }

  // Error covariance matrix:
  // P = [ p00  p01 ] [ p10  p11 ]
  // p00 = uncertainty of the estimated price level
  // p11 = uncertainty of the estimated velocity
  // p01 / p10 = covariance between price and velocity errors
  // Start with identity covariance, meaning both states initially have some uncertainty and are assumed uncorrelated.
  protected double p00 = 1;
  protected double p01 = 0;
  protected double p10 = 0;
  protected double p11 = 1;

  /// <summary>
  /// Process noise.
  /// Controls how quickly the filter is allowed to change its internal state independently of new observations.
  /// Higher value:
  /// - adapts faster
  /// - follows changing trends more closely
  /// - produces noisier velocity
  /// Lower value:
  /// - smoother
  /// - slower adaptation
  /// - more persistent trend estimate
  /// </summary>
  public double ProcessNoise = 0.0001;

  /// <summary>
  /// Observation noise.
  /// Represents how noisy/unreliable the incoming price observation is assumed to be.
  /// Higher value:
  /// - trust observations less
  /// - smoother result
  /// Lower value:
  /// - trust observations more
  /// - faster response
  /// </summary>
  public double ObservationNoise = 0.05;

  // Prevents the first observation from being treated as a normal
  // Kalman update before the initial state has been established.
  protected bool setup;

  public double Update(double price)
  {
    // There is no prior state for the first observation.
    // Use the first price as the initial level and assume zero velocity.
    // This avoids generating an artificial velocity from an arbitrary initial value such as zero.
    if (setup is false)
    {
      Mean = 0;
      setup = true;
      Value = price;

      return Mean;
    }

    // State transition:
    // Value(t) = Value(t-1) + Mean(t-1)
    // Mean(t)  = Mean(t-1)
    // Therefore: F = [ 1  1 ] [ 0  1 ]
    // The predicted covariance is:
    // P_pred = F P F' + Q
    // These four scalar expressions are the expanded form of that matrix multiplication, avoiding Matrix allocations in the hot Update() path.
    // Predicted uncertainty of the price level.
    // p00 + p10 + p01 + p11 is the expanded:
    // P00 + P01 + P10 + P11
    // from F * P * F'
    // ProcessNoise is added because the state itself can change unpredictably between observations.
    double p00_pred = p00 + p10 + p01 + p11 + ProcessNoise;

    // Predicted covariance between price and velocity.
    double p01_pred = p01 + p11;

    // Same covariance as p01 for a symmetric covariance matrix.
    double p10_pred = p10 + p11;

    // Predicted uncertainty of the velocity.
    // Process noise allows the estimated velocity to change over time.
    double p11_pred = p11 + ProcessNoise;

    // Predict the next price using the current estimated velocity.
    // This is: x_pred = F * x
    // specifically:
    // price_pred = Value + Mean
    // Notice that Mean is effectively the predicted price change over one observation interval.
    double price_pred = Value + Mean;

    // Innovation / residual:
    // y = observed price - predicted price
    // Positive y means the market moved above what the filter expected.
    // Negative y means it moved below the prediction.
    double y = price - price_pred;

    // Innovation covariance:
    // S = H P_pred H' + R
    // The observation is only the price component:
    // H = [1, 0]
    // therefore:
    // S = p00_pred + ObservationNoise
    // This represents the expected uncertainty of the prediction error.
    double S = p00_pred + ObservationNoise;

    // Kalman gain for the price state.
    // Determines how much the predicted price should move toward the actual observation.
    double K0 = p00_pred / S;

    // Kalman gain for the velocity state.
    // This is particularly important for this indicator: a persistent prediction error changes the estimated velocity.
    // If the covariance between price and velocity is large,
    // the filter interprets the price surprise as evidence that
    // the underlying trend/velocity has changed.
    double K1 = p10_pred / S;

    // Correct the predicted price using the innovation.
    // Value = predictedValue + K0 * error
    // K0 close to 1 -> trust the new price strongly.
    // K0 close to 0 -> retain the prediction.
    Value = price_pred + K0 * y;

    // Correct the estimated velocity.
    // Mean = predictedMean + K1 * error
    // Because the predicted velocity is simply the previous Mean, this can be written as:
    // Mean += K1 * y
    // A sequence of positive prediction errors increases Mean, a sequence of negative errors decreases Mean.
    Mean += K1 * y;

    // Standard Kalman covariance update:
    // P = (I - K H) P_pred
    // Since: H = [1, 0]
    // the matrix multiplication can again be expanded into scalar operations to avoid allocating matrices.
    // Remaining uncertainty of the price estimate after observing the new price.
    p00 = (1 - K0) * p00_pred;

    // Remaining covariance between price and velocity.
    p01 = (1 - K0) * p01_pred;

    // Remaining covariance in the opposite direction.
    p10 = p10_pred - K1 * p00_pred;

    // Remaining uncertainty of the velocity estimate.
    p11 = p11_pred - K1 * p01_pred;

    // Return the estimated velocity/trend rather than the filtered price.
    return Mean;
  }
}
