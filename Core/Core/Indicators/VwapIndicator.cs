using Core.Models; 
using System; 

namespace Core.Indicators
{
  public class VwapIndicator
  {
    // Number of standard deviations for bands
    public double Band { get; set; } = 2.0;

    // Cumulative volume: sum(V)
    protected double cumV;
    // Cumulative price*volume: sum(TypicalPrice * V)
    protected double cumPV;
    // Cumulative price^2*volume: sum(TypicalPrice^2 * V) for variance
    protected double cumPV2;

    // Called per tick/bar
    public virtual Price Update(Price point)
    {
      if (point.Bar.Low is not double L || point.Bar.High is not double H || point.Bar.Close is not double C)
      {
        return point;
      }

      var volume = point.Volume ?? 0;
      var price = (L + H + C) / 3.0;

      cumV += volume;
      cumPV += price * volume;
      cumPV2 += price * price * volume;

      // VWAP = sum(P*V) / sum(V)
      var vwap = cumPV / cumV;
      // Variance = E[P^2] - E[P]^2, clamp to 0 for floating point
      var variance = Math.Max(cumPV2 / cumV - vwap * vwap, 0);
      // Standard deviation = sqrt(variance)
      var deviation = Math.Sqrt(variance);

      return new()
      {
        Last = vwap,
        Bar = new()
        {
          High = vwap + Band * deviation,
          Low = vwap - Band * deviation
        }
      };
    }
  }
}
