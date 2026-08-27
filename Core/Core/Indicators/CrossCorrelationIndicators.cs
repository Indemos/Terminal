using System;

namespace Core.Indicators
{
  public class CrossCorrelationIndicator
  {
    // Circular-buffer position of the newest observation.
    // The buffer wraps around when it reaches Frame.
    protected int pointer;

    // Number of observations received so far.
    // Until this reaches Frame, the correlation window is not full.
    protected int count;

    // Previous prices used to calculate returns.
    protected double previousX;
    protected double previousY;

    // Circular buffers containing the most recent X/Y values.
    // The values are either prices or returns depending on UseReturns.
    protected double[] itemsX;
    protected double[] itemsY;

    // Maximum lag to test in either direction.
    // Example: Lag = 10 tests -10 ... +10 periods.
    public int Lag { get; set; }

    // Number of observations in the rolling correlation window.
    public int Frame { get; set; }

    // If true, correlate returns instead of raw prices.
    // Returns are generally preferable for lead/lag analysis because
    // price levels can be highly correlated simply because they trend.
    public bool UseReturns { get; set; }

    // Lag where the strongest absolute correlation was found.
    // Positive: X leads Y by this many periods.
    // Negative: Y leads X by |this| periods.
    public int LeadBias { get; protected set; }

    // Correlation corresponding to LeadBias.
    // The sign is preserved:
    // +1 = perfectly positive relationship
    // -1 = perfectly negative relationship
    public double MaxCorrelation { get; protected set; }

    public CrossCorrelationIndicator(int windowSize = 60, int maxLag = 10)
    {
      Frame = windowSize;

      // Do not allow the lag to consume more than half the window.
      // Otherwise a lag would leave too few overlapping observations to produce a meaningful correlation.
      Lag = Math.Min(maxLag, windowSize / 2);

      // Allocate the rolling buffers once.
      // No allocations are required during Update().
      itemsX = new double[windowSize];
      itemsY = new double[windowSize];
    }

    public virtual double Update(double priceX, double priceY)
    {
      // Convert prices to either:
      // - raw prices, when UseReturns == false
      // - period-to-period returns, when UseReturns == true
      var X = Item(ref previousX, priceX);
      var Y = Item(ref previousY, priceY);

      // Count the number of observations received.
      count++;

      // Move the circular-buffer head to the next position.
      // Example with Frame = 5:
      // 0 -> 1 -> 2 -> 3 -> 4 -> 0 -> ...
      // 'counter' always points to the newest value after this operation.
      pointer = (pointer + 1) % Frame;

      // Store the newest pair of observations.
      itemsX[pointer] = X;
      itemsY[pointer] = Y;

      // Don't calculate correlation until the rolling window contains Frame observations.
      // Before that point, older positions in the arrays contain uninitialized / default values and would distort the result.
      if (count < Frame)
      {
        LeadBias = 0;
        MaxCorrelation = 0.0;
        return 0.0;
      }

      // The window is full, so search all configured lags and find the lag producing the strongest correlation.
      Correlation();

      return MaxCorrelation;
    }

    protected virtual double Item(ref double prev, double curr)
    {
      // By default, correlate the actual price.
      var response = curr;

      if (UseReturns)
      {
        // Calculate the percentage price change from the previous observation:
        // return = (current - previous) / previous
        // On the first observation there is no previous price, so return zero.
        response = prev is 0 ? 0.0 : (curr - prev) / prev;

        // Save the current price for the next Update().
        prev = curr;
      }

      return response;
    }

    protected virtual void Correlation()
    {
      // Best result found while scanning all possible lags.
      var bestLag = 0;
      var bestCorr = 0.0;

      // Test every lag from -Lag to +Lag.
      // Positive lag: X leads Y.
      // Negative lag: Y leads X.
      // Lag = 0: X and Y are compared at the same observation.
      for (var i = -Lag; i <= Lag; i++)
      {
        // Running sums required to calculate Pearson correlation without creating temporary arrays.
        // Pearson correlation can be calculated as:
        // numerator = n * Sum(XY) - Sum(X) * Sum(Y)
        // denominator = sqrt([n * Sum(X²) - Sum(X)²] * [n * Sum(Y²) - Sum(Y)²])
        double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0, sumY2 = 0;

        // A non-zero lag reduces the number of observations that can be paired.
        // Example: Frame = 60, Lag = 10
        // Only 50 observations overlap.
        var n = Frame - Math.Abs(i);

        for (var ii = 0; ii < n; ii++)
        {
          // ii is the lookback distance from the newest observation.
          // ii = 0 -> newest value
          // ii = 1 -> previous value
          // ii = 2 -> value before that
          // Because the data is stored newest-first conceptually,
          // adding to the lookback moves further into the past.

          // Positive lag: X leads Y
          // We therefore compare an older X observation against a newer Y observation.
          // Example with lag = +2:
          // X: [older] [older] [current]
          // Y: [older] [current]
          // X is shifted two periods into the past relative to Y.
          // Negative lag: Y leads X
          // so Y gets the additional lookback.
          var xLoop = (i > 0) ? ii + i : ii;
          var yLoop = (i > 0) ? ii : ii - i;

          // Convert the lookback position into a circular-buffer index.
          // pointer = newest element
          // - xLoop: moves backward through time.
          // + Frame: prevents a negative value before applying modulo.
          // % Frame: wraps around the circular buffer.
          var x = itemsX[(pointer - xLoop + Frame) % Frame];
          var y = itemsY[(pointer - yLoop + Frame) % Frame];

          // Accumulate the terms needed for Pearson correlation.
          sumX += x;
          sumY += y;
          sumXY += x * y;
          sumX2 += x * x;
          sumY2 += y * y;
        }

        // Calculate the two variance-related terms of Pearson's correlation denominator.
        // These are proportional to:
        // Sum((X - meanX)²)
        // Sum((Y - meanY)²)
        // If either is zero, one of the series is constant within the overlapping window and correlation is undefined.
        var denominator = (n * sumX2 - sumX * sumX) * (n * sumY2 - sumY * sumY);

        if (denominator <= 0)
        {
          continue;
        }

        // corr = (n * Sum(XY) - Sum(X)Sum(Y)) / sqrt(varianceTermX * varianceTermY)
        // The result is in [-1, +1].
        var corr = ((n * sumXY) - (sumX * sumY)) / Math.Sqrt(denominator);

        // We are looking for the strongest relationship regardless of direction, so compare absolute correlation values.
        // However, preserve the original sign in bestCorr.
        // Example:
        // corr = +0.70 -> strong positive relationship
        // corr = -0.85 -> even stronger relationship
        // Therefore -0.85 wins because |-0.85| > |+0.70|.
        if (Math.Abs(corr) > Math.Abs(bestCorr))
        {
          bestLag = i;
          bestCorr = corr;
        }
      }

      LeadBias = bestLag;
      MaxCorrelation = bestCorr;
    }
  }
}
