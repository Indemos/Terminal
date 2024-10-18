using MathNet.Numerics.LinearAlgebra;
using System;
using System.Linq;

namespace Terminal.Core.Services
{
  public class CointegrationService
  {
    /// <summary>
    /// First difference for the time series
    /// </summary>
    /// <param name="matrix"></param>
    /// <returns></returns>
    static Matrix<double> Integrate(Matrix<double> matrix)
    {
      var rows = matrix.RowCount;
      var cols = matrix.ColumnCount;
      var response = Matrix<double>.Build.Dense(rows - 1, cols);

      for (var i = 1; i < rows; i++)
      {
        response.SetRow(i - 1, matrix.Row(i) - matrix.Row(i - 1));
      }

      return response;
    }

    /// <summary>
    /// Get lag matrix
    /// </summary>
    /// <param name="matrix"></param>
    /// <param name="lags"></param>
    /// <returns></returns>
    static Matrix<double> Lag(Matrix<double> matrix, int lags)
    {
      var rows = matrix.RowCount - lags;
      var cols = matrix.ColumnCount * lags;
      var response = Matrix<double>.Build.Dense(rows, cols);

      for (var i = 1; i <= lags; i++)
      {
        var section = matrix.SubMatrix(i, rows, 0, matrix.ColumnCount);
        response.SetSubMatrix(0, rows, (i - 1) * matrix.ColumnCount, matrix.ColumnCount, section);
      }

      return response;
    }

    /// <summary>
    /// Cointegration
    /// </summary>
    /// <param name="Y"></param>
    /// <param name="lags"></param>
    /// <returns></returns>
    public static double Johansen(Matrix<double> Y, int lags = 1)
    {
      // Step 1: First difference of Y
      var dY = Integrate(Y);

      // Step 2: Create lagged Y (Y_{t-1}, ..., Y_{t-lags})
      var lagY = Lag(Y, lags);

      // Step 3: Compute residuals from OLS of dY on laggedY
      var beta = OLS(lagY, dY);  // β = (X'X)^(-1) X'Y
      var dYhat = lagY * beta;
      var residuals = dY - dYhat;

      // Step 4: Compute covariance matrices for residuals
      var S11 = residuals.TransposeThisAndMultiply(residuals) / residuals.RowCount;
      var S00 = lagY.TransposeThisAndMultiply(lagY) / lagY.RowCount;

      // Step 5: Compute eigenvalues and eigenvectors
      var S00Inv = S00.Inverse();
      var S00InvS11 = S00Inv * S11;
      var evd = S00InvS11.Evd();
      var eigenValues = evd.EigenValues.Real();

      // Step 6: Compute Trace and Max-Eigenvalue statistics
      var traceTests = eigenValues.Select(o => -Math.Log(1 - o)).ToList();
      var traceStat = traceTests.Sum();

      // Step 7: Compare trace statistics with critical values to determine cointegration rank
      // github.com/iisayoo/johansen/blob/master/johansen/critical_values.py
      // Critical values for Johansen's Trace test at a 95% confidence level
      var criticalValues = new double[] { 2.98, 4.13, 6.94, 10.47, 12.32, 16.36, 21.78, 24.28, 29.51 };
      var cointegrationRank = 0;

      for (var i = 0; i < traceTests.Count; i++)
      {
        Console.WriteLine(traceTests[i] + " > " + criticalValues[i]);

        if (traceTests[i] > criticalValues[i])
        {
          cointegrationRank++;
        }
      }

      return cointegrationRank;
    }

    /// <summary>
    /// OLS Regression (Ordinary Least Squares): β = (X'X)^(-1) X'Y
    /// </summary>
    /// <param name="X"></param>
    /// <param name="Y"></param>
    /// <returns></returns>
    static Matrix<double> OLS(Matrix<double> X, Matrix<double> Y)
    {
      var Xt = X.Transpose();
      var XtXInv = (Xt * X).Inverse();
      return XtXInv * Xt * Y;
    }
  }
}
