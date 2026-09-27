public class VelocityIndicator
{
  public double Value { get; private set; }
  public double Velocity { get; private set; } // this is zero-mean, but trend following
  private double p00 = 1, p01 = 0, p10 = 0, p11 = 1;
  public double ProcessNoise = 0.0001;
  public double ObservationNoise = 0.05;
  private bool setup;

  public double Update(double price)
  {
    if (!setup) { Value = price; Velocity = 0; setup = true; return Velocity; }

    // PREDICT: F = [[1,1],[0,1]]
    double p00_pred = p00 + p10 + p01 + p11 + ProcessNoise;
    double p01_pred = p01 + p11;
    double p10_pred = p10 + p11;
    double p11_pred = p11 + ProcessNoise;

    double price_pred = Value + Velocity;

    // UPDATE
    double y = price - price_pred;
    double S = p00_pred + ObservationNoise;
    double K0 = p00_pred / S;
    double K1 = p10_pred / S;

    Value = price_pred + K0 * y;
    Velocity += K1 * y; // <- now it actually moves

    // P = (I-KH)P_pred
    p00 = (1 - K0) * p00_pred;
    p01 = (1 - K0) * p01_pred;
    p10 = p10_pred - K1 * p00_pred;
    p11 = p11_pred - K1 * p01_pred;

    return Velocity;
  }
}
