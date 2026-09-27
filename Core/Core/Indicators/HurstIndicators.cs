using System;
using System.Collections.Generic;

namespace Core.Indicators
{
  public class HurstIndicator
  {
    protected bool setup;
    protected Queue<double> prices = new();

    public virtual int Period { get; set; } = 100;
    public virtual int[] Lags { get; set; } = [ 10, 20, 40, 80 ];
    public virtual double Value { get; protected set; } = 0.5;
    public virtual double Mean { get; protected set; }

    /// <summary>
    /// Update
    /// </summary>
    /// <param name="price"></param>
    public virtual double Update(double price)
    {
      prices.Enqueue(price);

      if (prices.Count > Period)
      {
        prices.Dequeue();
      }

      if (prices.Count < Period)
      {
        Mean = price;
        setup = false;
        return Value = 0.5;
      }

      setup = true;

      var arr = prices.ToArray();
      var sum = 0.0;

      foreach (var p in arr) sum += p;

      Mean = sum / Period;

      var xs = new List<double>();
      var ys = new List<double>();

      foreach (var n in Lags)
      {
        if (n < 10 || n > Period)
        {
          continue;
        }

        var chunks = Period / n;

        if (chunks is 0)
        {
          continue;
        }

        var sumRs = 0.0;
        var valid = 0;

        for (var c = 0; c < chunks; c++)
        {
          var start = c * n;
          var mean = 0.0;

          for (var i = 0; i < n; i++) mean += arr[start + i];

          mean /= n;

          var cum = 0.0;
          var sumSquare = 0.0;
          var cumMax = double.MinValue;
          var cumMin = double.MaxValue;

          for (var i = 0; i < n; i++)
          {
            var dev = arr[start + i] - mean;

            cum += dev;
            sumSquare += dev * dev;

            if (cum > cumMax) cumMax = cum;
            if (cum < cumMin) cumMin = cum;
          }

          var range = cumMax - cumMin;
          var std = Math.Sqrt(sumSquare / n);

          if (std < 1e-12 || range < 1e-12)
          {
            continue;
          }

          var rs = range / std;

          if (rs <= 0)
          {
            continue;
          }

          sumRs += rs;
          valid++;
        }

        if (valid is 0) continue;

        var avgRs = sumRs / valid;

        xs.Add(Math.Log(n));
        ys.Add(Math.Log(avgRs));
      }

      if (xs.Count < 2)
      {
        return Value = 0.5;
      }

      // slope = Cov(x,y)/Var(x)
      var count = xs.Count;
      var sumX = 0.0; var sumY = 0.0; var sumXY = 0.0; var sumX2 = 0.0;

      for (var i = 0; i < count; i++)
      {
        sumX += xs[i];
        sumY += ys[i];
        sumXY += xs[i] * ys[i];
        sumX2 += xs[i] * xs[i];
      }

      var denom = count * sumX2 - sumX * sumX;

      if (Math.Abs(denom) < 1e-12)
      {
        return Value = 0.5;
      }

      var response = (count * sumXY - sumX * sumY) / denom;

      // don't force 0/1 binary, only soft clamp
      if (response < 0) response = 0;
      if (response > 1) response = 1;

      return Value = response;
    }
  }
}
