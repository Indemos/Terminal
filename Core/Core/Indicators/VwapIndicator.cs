using Core.Models;
using System;

public class VwapIndicator
{
  // Cumulative volume: sum(V)
  protected double? cumV = 0;
  // Cumulative price*volume: sum(Price * V)
  protected double? cumPV = 0;
  // Cumulative price^2*volume: sum(Price^2 * V) for variance
  protected double? cumPV2 = 0;

  // Number of standard deviations for bands
  public double Band { get; set; } = 2.0;

  public virtual Price Update(Price price)
  {
    if (price.Volume <= 0)
    {
      return new();
    }

    cumV += price.Volume;
    cumPV += price.Last * price.Volume;
    cumPV2 += price.Last * price.Last * price.Volume;

    // VWAP = sum(P * V) / sum(V)
    // Variance = E[P^2] - E[P]^2
    // Standard deviation = sqrt(variance)

    var vwap = cumPV / cumV;
    var variance = cumPV2 / cumV - vwap * vwap;
    var deviation = Math.Sqrt(variance.Value);

    return new()
    {
      Last = vwap,
      Bar = new()
      {
        Low = vwap - Band * deviation,
        High = vwap + Band * deviation
      }
    };
  }
}
