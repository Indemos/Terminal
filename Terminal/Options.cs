using MathNet.Numerics.Distributions;
using MathNet.Numerics.RootFinding;
using System;
using Terminal.Core.Enums;

namespace Terminal
{
  public class BlackScholes
  {
    private static double D1(double S, double K, double T, double sigma, double r, double q)
    {
      return (Math.Log(S / K) + (r - q + (sigma * sigma) / 2) * T) / (sigma * Math.Sqrt(T));
    }

    private static double D2(double T, double sigma, double d1)
    {
      return d1 - sigma * Math.Sqrt(T);
    }

    /// <summary>
    /// Computes theoretical price.
    /// </summary>
    /// <param name="optionType">call or put</param>
    /// <param name="S">Underlying price</param>
    /// <param name="K">Strike price</param>
    /// <param name="T">Time to expiration in % of year</param>
    /// <param name="sigma">Volatility</param>
    /// <param name="r">continuously compounded risk-free interest rate</param>
    /// <param name="q">continuously compounded dividend yield</param>
    /// <returns></returns>
    public static double Premium(OptionSideEnum optionType, double S, double K, double T, double sigma, double r, double q)
    {
      double d1 = D1(S, K, T, sigma, r, q);
      double d2 = D2(T, sigma, d1);

      switch (optionType)
      {
        case OptionSideEnum.Call:
          return S * Math.Exp(-q * T) * Normal.CDF(0, 1, d1) - K * Math.Exp(-r * T) * Normal.CDF(0, 1, d2);

        case OptionSideEnum.Put:
          return K * Math.Exp(-r * T) * Normal.CDF(0, 1, -d2) - S * Math.Exp(-q * T) * Normal.CDF(0, 1, -d1);
      }

      return 0;
    }

    /// <summary>
    /// Computes Vega. The amount of option price change for each 1% change in vol (sigma)
    /// </summary>
    /// <param name="S">Underlying price</param>
    /// <param name="K">Strike price</param>
    /// <param name="T">Time to expiration in % of year</param>
    /// <param name="sigma">Volatility</param>
    /// <param name="r">continuously compounded risk-free interest rate</param>
    /// <param name="q">continuously compounded dividend yield</param>
    /// <returns></returns>
    public static double Vega(double S, double K, double T, double sigma, double r, double q)
    {
      double d1 = D1(S, K, T, sigma, r, q);
      double vega = S * Math.Exp(-q * T) * Normal.PDF(0, 1, d1) * Math.Sqrt(T);
      return vega / 100;
    }

    public static double IV(OptionSideEnum optionType, double S, double K, double T, double r, double q, double optionMarketPrice)
    {
      Func<double, double> f = sigma => Premium(optionType, S, K, T, sigma, r, q) - optionMarketPrice;
      Func<double, double> df = sigma => Vega(S, K, T, sigma, r, q);

      return RobustNewtonRaphson.FindRoot(f, df, lowerBound: 0, upperBound: 100, accuracy: 0.001);
    }

    /// <summary>
    /// Computes theta.
    /// </summary>
    /// <param name="optionType">call or put</param>
    /// <param name="S">Underlying price</param>
    /// <param name="K">Strike price</param>
    /// <param name="T">Time to expiration in % of year</param>
    /// <param name="sigma">Volatility</param>
    /// <param name="r">continuously compounded risk-free interest rate</param>
    /// <param name="q">continuously compounded dividend yield</param>
    /// <returns></returns>
    public static double Theta(OptionSideEnum optionType, double S, double K, double T, double sigma, double r, double q)
    {
      double d1 = D1(S, K, T, sigma, r, q);
      double d2 = D2(T, sigma, d1);

      switch (optionType)
      {
        case OptionSideEnum.Call:
          {
            double theta = -Math.Exp(-q * T) * (S * Normal.PDF(0, 1, d1) * sigma) / (2.0 * Math.Sqrt(T))
                    - (r * K * Math.Exp(-r * T) * Normal.CDF(0, 1, d2))
                    + q * S * Math.Exp(-q * T) * Normal.CDF(0, 1, d1);

            return theta / 365;
          }

        case OptionSideEnum.Put:
          {
            double theta = -Math.Exp(-q * T) * (S * Normal.PDF(0, 1, d1) * sigma) / (2.0 * Math.Sqrt(T))
                + (r * K * Math.Exp(-r * T) * Normal.PDF(0, 1, -d2))
                - q * S * Math.Exp(-q * T) * Normal.CDF(0, 1, -d1);

            return theta / 365;
          }

        default:
          throw new NotSupportedException();
      }
    }

    /// <summary>
    /// Computes delta.
    /// </summary>
    /// <param name="optionType">call or put</param>
    /// <param name="S">Underlying price</param>
    /// <param name="K">Strike price</param>
    /// <param name="T">Time to expiration in % of year</param>
    /// <param name="sigma">Volatility</param>
    /// <param name="r">continuously compounded risk-free interest rate</param>
    /// <param name="q">continuously compounded dividend yield</param>
    /// <returns></returns>
    public static double Delta(OptionSideEnum optionType, double S, double K, double T, double sigma, double r, double q)
    {
      double d1 = D1(S, K, T, sigma, r, q);

      switch (optionType)
      {
        case OptionSideEnum.Call:
          return Math.Exp(-r * T) * Normal.CDF(0, 1, d1);

        case OptionSideEnum.Put:
          return -Math.Exp(-r * T) * Normal.CDF(0, 1, -d1);

        default:
          throw new NotSupportedException();
      }
    }

    /// <summary>
    /// Computes gamma.
    /// </summary>
    /// <param name="S">Underlying price</param>
    /// <param name="K">Strike price</param>
    /// <param name="T">Time to expiration in % of year</param>
    /// <param name="sigma">Volatility</param>
    /// <param name="r">continuously compounded risk-free interest rate</param>
    /// <param name="q">continuously compounded dividend yield</param>
    /// <returns></returns>
    public static double Gamma(double S, double K, double T, double sigma, double r, double q)
    {
      double d1 = D1(S, K, T, sigma, r, q);
      return Math.Exp(-q * T) * (Normal.PDF(0, 1, d1) / (S * sigma * Math.Sqrt(T)));
    }

    /// <summary>
    /// Computes delta.
    /// </summary>
    /// <param name="optionType">call or put</param>
    /// <param name="S">Underlying price</param>
    /// <param name="K">Strike price</param>
    /// <param name="T">Time to expiration in % of year</param>
    /// <param name="sigma">Volatility</param>
    /// <param name="r">continuously compounded risk-free interest rate</param>
    /// <param name="q">continuously compounded dividend yield</param>
    /// <returns></returns>
    public static double Rho(OptionSideEnum optionType, double S, double K, double T, double sigma, double r, double q)
    {
      double d1 = D1(S, K, T, sigma, r, q);
      double d2 = D2(T, sigma, d1);

      switch (optionType)
      {
        case OptionSideEnum.Call:
          return K * T * Math.Exp(-r * T) * Normal.CDF(0, 1, d2);

        case OptionSideEnum.Put:
          return -K * T * Math.Exp(-r * T) * Normal.CDF(0, 1, -d2);

        default:
          throw new NotSupportedException();
      }
    }
  }
}
