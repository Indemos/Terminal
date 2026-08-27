using Core.Models;
using System;
using System.Collections.Generic;

namespace Core.Indicators
{
  /// <summary>
  /// Defines how the series is normalized.
  /// </summary>
  public enum ScaleMode
  {
    /// <summary>
    /// Current price relative to Pin.
    /// Price / Pin
    /// </summary>
    Pin,

    /// <summary>
    /// Current price relative to Pin, normalized by recent volatility.
    /// Log(Price / Pin) / Volatility
    /// </summary>
    Instant,

    /// <summary>
    /// Changes in the current price relative to Pin, normalized by recent volatility and accumulated.
    /// Produces a synthetic price-like series.
    /// </summary>
    Accumulation
  }

  /// <summary>
  /// Price normalizer supporting three modes:
  /// Pin: Price / Pin * Scale
  /// Instant: Log(Price / Pin) / Volatility * Scale
  /// Accumulation: Accumulated reference-relative movement normalized by volatility.
  /// In all modes the current value is ultimately compared against Pin.
  /// Volatility is calculated from the last Period logarithmic returns.
  /// </summary>
  public class ScaleIndicator
  {
    /// <summary>
    /// Defines how the series is normalized.
    /// </summary>
    public virtual ScaleMode Mode { get; set; } = ScaleMode.Pin;

    /// <summary>
    /// Output multiplier.
    /// Volatility modes: multiplier applied to the normalized result.
    /// </summary>
    public virtual double Scale { get; set; } = 1.0;

    /// <summary>
    /// Reference value used for normalization.
    ///
    /// If null, the first observed value becomes the reference.
    /// </summary>
    public virtual double? Pin { get; set; }

    /// <summary>
    /// Number of returns used to calculate volatility.
    /// This affects only the volatility estimate.
    /// It does not limit accumulation.
    /// </summary>
    public virtual int Period { get; set; } = 60;

    /// <summary>
    /// Use logarithmic returns for volatility calculation.
    /// Logarithmic returns require positive values.
    /// If false, arithmetic differences are used instead, allowing zero and negative values.
    /// </summary>
    public virtual bool LogReturns { get; set; } = true;

    /// <summary>
    /// Previous value used only for calculating volatility.
    /// It is NOT used as the normalization reference.
    /// </summary>
    protected double? previousItem;

    /// <summary>
    /// Accumulated normalized value.
    /// </summary>
    protected double accumulation;

    /// <summary>
    /// Rolling returns used for volatility estimation.
    /// </summary>
    protected readonly Queue<double> steps = new();

    /// <summary>
    /// Sum of returns in the volatility window.
    /// </summary>
    protected double summary;

    /// <summary>
    /// Sum of squared returns in the volatility window.
    /// </summary>
    protected double summarySquare;

    /// <summary>
    /// Calculate the normalized value.
    /// </summary>
    public virtual double? Update(Price currentPoint)
    {
      var value = currentPoint.Last;

      Pin ??= value.Value;
      previousItem ??= value;

      switch (Mode)
      {
        case ScaleMode.Instant: return UpdateInstant(value.Value);
        case ScaleMode.Accumulation: return UpdateAccumulation(value.Value);
      }

      return UpdatePin(value.Value);
    }

    /// <summary>
    /// Reference-based normalization.
    /// value = Price / Pin * Scale
    /// This is the original normalization.
    /// </summary>
    protected virtual double UpdatePin(double value)
    {
      return Pin is null or 0 ? 0 : value / Pin.Value * Scale;
    }

    /// <summary>
    /// Reference-relative volatility normalization.
    /// referenceReturn = Log(Price / Pin)
    /// normalized = referenceReturn / volatility
    /// The current value is compared against Pin, NOT the previous value.
    /// </summary>
    protected virtual double UpdateInstant(double value)
    {
      var step = PinStep(value);
      var range = Range(value);

      if (range is 0)
      {
        return 0.0;
      }

      return step / range * Scale;
    }

    /// <summary>
    /// Accumulated reference-relative volatility normalization.
    /// The current value is always represented relative to Pin.
    /// The change in the reference-relative value is normalized by volatility and accumulated. 
    /// This produces a price-like series while preserving the reference-relative direction.
    /// </summary>
    protected virtual double UpdateAccumulation(double value)
    {
      var change = PinStep(value) - PinStep(previousItem.Value);
      var range = Range(value);

      if (range is 0)
      {
        return accumulation;
      }

      accumulation += change / range * Scale;

      return accumulation;
    }

    /// <summary>
    /// Calculates the return between the current value and the previous value.
    /// This is used ONLY to estimate volatility.
    /// It is not used as the normalization reference.
    /// Logarithmic: Log(P[t] / P[t-1])
    /// Arithmetic: P[t] - P[t-1]
    /// </summary>
    protected virtual double Step(double value)
    {
      if (LogReturns)
      {
        return Math.Log(value / previousItem.Value);
      }

      return value - previousItem.Value;
    }

    /// <summary>
    /// Calculates the current value relative to Pin.
    /// Logarithmic: Log(P[t] / Pin)
    /// Arithmetic: P[t] - Pin
    /// This is the normalization basis for the volatility modes.
    /// </summary>
    protected virtual double PinStep(double value)
    {
      if (LogReturns)
      {
        return Math.Log(value / Pin.Value);
      }

      return value - Pin.Value;
    }

    /// <summary>
    /// Adds a return to the rolling volatility window.
    /// </summary>
    protected virtual void Append(double value)
    {
      steps.Enqueue(value);

      summary += value;
      summarySquare += value * value;

      while (steps.Count > Period)
      {
        var next = steps.Dequeue();

        summary -= next;
        summarySquare -= next * next;
      }
    }

    /// <summary>
    /// Calculates rolling standard deviation of returns.
    /// </summary>
    /// <param name="value"></param>
    protected virtual double Range(double value)
    {
      Append(Step(value));

      previousItem = value;

      var count = steps.Count;
      var mean = summary / count;
      var variance = summarySquare / count - mean * mean;

      return Math.Sqrt(Math.Max(0.0, variance));
    }
  }
}
