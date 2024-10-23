using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Factorization;
using System;
using System.Linq;

namespace Terminal.Core.Services
{
  /// <summary>
  /// References
  /// Numerically Stable Cointegration Analysis - Jurgen Doornik
  /// Alternative asymptotics for cointegration tests in large VARs - Alexei Onatski and Chen Wang
  /// Testing for Cointegration Using the Johansen Methodology when Variables are Near-Integrated - Erik Hjalmarsson
  /// Time Series Analysis - Hamilton, J.D.
  /// https://github.com/iisayoo/johansen/blob/master/johansen/johansen.py
  /// </summary>
  public class CointegrationService
  {
    /// <summary>
    /// Cointegration
    /// </summary>
    /// <param name="inputMatrix"></param>
    /// <param name="lags"></param>
    /// <returns></returns>
    public static (double, Vector<double>) Johansen(Matrix<double> inputMatrix, int lags = 1)
    {
      var matrix = Center(inputMatrix);
      var variables = matrix.ColumnCount;
      var observations = matrix.RowCount - Math.Max(1, lags);

      // Step 1: First difference
      var xSub =
        matrix.SubMatrix(1, matrix.RowCount - 1, 0, variables) -
        matrix.SubMatrix(0, matrix.RowCount - 1, 0, variables);

      // Step 2: Create lagged matrices
      var xLag = Lag(matrix, lags);
      var xSubLag = Lag(xSub, lags);

      // Step 3: Resize matrices
      xSub = xSub.Resize(observations, xSub.ColumnCount);
      xLag = xLag.Resize(observations, xLag.ColumnCount);
      xSubLag = xSubLag.Resize(observations, xSubLag.ColumnCount);

      // Step 4: Compute residuals of xSub and xLag on xSubLag
      var xSubLagInv = xSubLag.PseudoInverse();
      var u = xSub - xSubLag * xSubLagInv * xSub;
      var v = xLag - xSubLag * xSubLagInv * xLag;

      // Step 5: Compute eigenvalues and eigenvectors
      var Suu = u.TransposeThisAndMultiply(u) / observations;
      var Svv = v.TransposeThisAndMultiply(v) / observations;
      var Suv = u.TransposeThisAndMultiply(v) / observations;
      var evd = (Svv.PseudoInverse() * Suv.TransposeThisAndMultiply(Suu.PseudoInverse() * Suv)).Evd();
      var eigenValues = evd.EigenValues.Real();
      var maxIndex = eigenValues.MaximumIndex();
      var eigenVectors = evd.EigenVectors.Column(maxIndex);

      // Step 6: Compare trace statistics with critical values to determine cointegration rank
      var rank = GetRank(eigenValues, observations);

      return (rank, eigenVectors);
    }

    /// <summary>
    /// Subtract mean
    /// </summary>
    /// <param name="matrix"></param>
    /// <returns></returns>
    private static Matrix<double> Center(Matrix<double> matrix)
    {
      var columnMeans = matrix.ColumnSums() / matrix.RowCount;
      var mean = Matrix<double>.Build.Dense(matrix.RowCount, matrix.ColumnCount, (i, ii) => columnMeans[ii]);
      return matrix - mean;
    }

    /// <summary>
    /// Get approximated critical value for specified rank
    /// </summary>
    /// <param name="vars"></param>
    /// <param name="rank"></param>
    /// <returns></returns>
    private static double GetCriticalValue(int rank, int numVariables)
    {
      // Higher base critical value for better scaling
      double baseCriticalValue = 3.76;

      // Stronger scaling factors to match real-world growth in critical values
      double variableFactor = Math.Pow(2.5, numVariables - 2);  // More aggressive scaling for variables
      double rankFactor = Math.Pow(2.5, rank);  // Steep scaling for rank

      // Final critical value is the base value multiplied by both factors
      double criticalValue = baseCriticalValue * variableFactor * rankFactor;

      return criticalValue;
    }

    /// <summary>
    /// Lag matrix
    /// </summary>
    /// <param name="matrix"></param>
    /// <param name="lags"></param>
    /// <param name="sub"></param>
    /// <returns></returns>
    private static Matrix<double> Lag(Matrix<double> matrix, int lags = 1)
    {
      var response = Matrix<double>.Build.Dense(matrix.RowCount, matrix.ColumnCount * lags);

      for (var i = 0; i < lags; i++)
      {
        var subMatrix = matrix.SubMatrix(0, matrix.RowCount - i, 0, matrix.ColumnCount);
        response.SetSubMatrix(i + 1, subMatrix.RowCount - 1, matrix.ColumnCount * i, subMatrix.ColumnCount, subMatrix);
      }

      return response;
    }

    /// <summary>
    /// Get
    /// </summary>
    /// <param name="eigenValues"></param>
    /// <param name="observations"></param>
    /// <returns></returns>
    private static int GetRank(Vector<double> eigenValues, int observations)
    {
      var variables = eigenValues.Count;

      // Loop through from rank r to m (the number of variables)
      for (var i = 0; i < variables; i++)
      {
        // Calculate the test statistic: -t * sum(log(1 - eigenvalues[i:]))
        var statistic = -observations * eigenValues.SubVector(i + 1, variables - i - 1).Sum(o => Math.Log(1 - o));
        // Get the corresponding critical value for this rank
        var critValue = GetCriticalValue(i, variables);

        // If the statistic is less than the critical value, return the rank i
        if (statistic < critValue)
        {
          return i;
        }
      }

      // If no rank satisfies the condition, return m
      return variables;
    }
  }
}
