using Core.Models;
using System;
using System.Collections.Generic;

namespace Core.Indicators
{
  public enum ScaleMode
  {
    /// <summary>
    /// Current price relative to the price 'Period' bars ago.
    /// Price / Reference
    /// </summary>
    Pin,

    /// <summary>
    /// Return over 'Period' bars, smoothly bounded within [-1, 1].
    /// Tanh(Log(Price / Reference))
    /// </summary>
    Mean
  }

  /// <summary>
  /// Time series normalizer using a rolling Period lookback
  /// </summary>
  public class ScaleIndicator
  {
    public virtual ScaleMode Mode { get; set; } = ScaleMode.Pin;

    /// <summary>
    /// The number of bars in the past to use as the reference point.
    /// </summary>
    public virtual int Period { get; set; } = 10;

    /// <summary>
    /// Current value
    /// </summary>
    public virtual double? Value { get; protected set; }

    /// <summary>
    /// Use logarithmic returns for calculation.
    /// Logarithmic returns require positive values.
    /// If false, arithmetic differences are used instead.
    /// </summary>
    public virtual bool Logs { get; set; } = true;

    /// <summary>
    /// The value from 'Period' bars ago, replacing the static Pin.
    /// </summary>
    protected double? pin;

    /// <summary>
    /// Rolling window to track historical prices.
    /// </summary>
    protected Queue<double> items = new();

    /// <summary>
    /// Calculate the normalized value.
    /// </summary>
    public virtual double? Update(Price currentPoint)
    {
      var value = currentPoint.Last.Value;

      pin ??= value;

      // 1. Maintain the rolling window
      items.Enqueue(value);

      // 2. Set the reference item to the price 'Period' bars ago
      if (items.Count > Period)
      {
        pin = items.Dequeue();
      }

      // 3. Calculate based on Mode
      switch (Mode)
      {
        case ScaleMode.Mean: return Value = Math.Tanh(Step(value));
        case ScaleMode.Pin: return Value = Step(value);
      }

      return null;
    }

    /// <summary>
    /// Calculates the return between the current value and the reference value from 'Period' bars ago.
    /// Logarithmic: Log(P[t] / P[t-Period])
    /// Arithmetic: P[t] - P[t-Period]
    /// </summary>
    protected virtual double Step(double value)
    {
      if (pin is null or 0)
      {
        return 0;
      }

      if (Logs)
      {
        return Math.Log(value / pin.Value);
      }

      return value - pin.Value;
    }
  }
}
