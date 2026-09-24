using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.Indicators
{
  public class AutoCorrIndicator
  {
    protected Queue<double> prices = new();
    protected Queue<double> returns = new();
    protected double? prevPrice = null;

    public virtual int Period { get; set; } = 50;
    public virtual double Value { get; protected set; } = 0;   // Lag-1 Autocorrelation
    public virtual double Mean { get; protected set; } = 0;    // Rolling Price Mean
    public virtual double StdDev { get; protected set; } = 0;  // Rolling Price StdDev

    public virtual void Update(double price)
    {
      // 1. Update Price Mean and StdDev
      prices.Enqueue(price);
      if (prices.Count > Period) prices.Dequeue();

      if (prices.Count >= 2)
      {
        double sum = 0;
        foreach (var p in prices) sum += p;
        Mean = sum / prices.Count;

        double sumSq = 0;
        foreach (var p in prices) sumSq += (p - Mean) * (p - Mean);
        StdDev = Math.Sqrt(sumSq / prices.Count);
      }

      // 2. Update Returns and Autocorrelation
      if (prevPrice.HasValue && prevPrice.Value != 0)
      {
        double ret = (price - prevPrice.Value) / prevPrice.Value;
        returns.Enqueue(ret);
        if (returns.Count > Period) returns.Dequeue();
      }
      prevPrice = price;

      // 3. Calculate Lag-1 Autocorrelation
      if (returns.Count >= Period)
      {
        var arr = returns.ToArray();
        double meanRet = arr.Average();
        double variance = arr.Select(x => Math.Pow(x - meanRet, 2)).Average();

        if (variance < 1e-12)
        {
          Value = 0;
          return;
        }

        double covariance = 0;
        for (int i = 0; i < arr.Length - 1; i++)
        {
          covariance += (arr[i] - meanRet) * (arr[i + 1] - meanRet);
        }
        covariance /= (arr.Length - 1);

        Value = covariance / variance;
      }
    }
  }
}
