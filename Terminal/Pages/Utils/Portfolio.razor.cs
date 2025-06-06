using Canvas.Core.Composers;
using Canvas.Core.Engines;
using Canvas.Core.Shapes;
using Canvas.Views.Web.Views;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using MudBlazor;
using Schwab;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Core.Domains;
using Terminal.Core.Models;
using Terminal.Core.Services;
using static alglib;

namespace Terminal.Pages.Utils
{
  public partial class Portfolio
  {
    enum RegressionType
    {
      Linear,
      NonLinear
    }

    int slope = 0;
    int samples = 250;
    string[] names = ["SPY", "VXX"];
    RegressionType regression = RegressionType.Linear;

    [Inject] ISnackbar Snackbar { get; set; }
    [Inject] IConfiguration Configuration { get; set; }

    protected CanvasView View { get; set; }

    /// <summary>
    /// API query to get options by criteria
    /// </summary>
    /// <returns></returns>
    protected override async Task OnAfterRenderAsync(bool setup)
    {
      if (setup)
      {
        var adapter = new Adapter
        {
          Account = new Account
          {
            Descriptor = Configuration.GetValue<string>("Schwab:Account")
          },
          ClientId = Configuration.GetValue<string>("Schwab:ConsumerKey"),
          ClientSecret = Configuration.GetValue<string>("Schwab:ConsumerSecret"),
          RefreshToken = Configuration.GetValue<string>("Schwab:RefreshToken"),
          AccessToken = Configuration.GetValue<string>("Schwab:AccessToken")
        };

        await adapter.Connect();

        var points = new Dictionary<string, IList<PointModel>>();

        foreach (var name in names)
        {
          var assetPoints = await adapter.GetPoints(new ConditionModel
          {
            ["period"] = 1,
            ["frequency"] = 1,
            ["symbol"] = name,
            ["periodType"] = "year",
            ["frequencyType"] = "daily"
          });

          points[name] = assetPoints
            .Data
            .Where(o => o.Last is not null)
            .TakeLast(samples)
            .ToList();
        }

        await Show(View, points, samples, slope);
      }
    }

    protected async Task Show(CanvasView view, IDictionary<string, IList<PointModel>> items, int itemsCount, double slope = 0)
    {
      var modelPoints = new List<double>();
      var comPoints = GetComparablePoints(items);
      var count = Math.Min(itemsCount, comPoints.Min(o => o.Value.Count));

      switch (true)
      {
        case true when slope is 0: modelPoints = GetSpread(count, 5, 1, 1, 0); break;
        case true when slope is not 0: modelPoints = GetSlope(count, slope); break;
      }

      var weights = comPoints.Select(o => 1.0 / comPoints.Count).ToArray();

      switch (regression)
      {
        case RegressionType.Linear: weights = GetLinearWeights(comPoints, [.. modelPoints]); break;
        case RegressionType.NonLinear: weights = GetNonLinearWeights(comPoints, [.. modelPoints]); break;
      }

      var chartPoints = modelPoints.Select((o, i) =>
      {
        var sum = 0.0;

        for (var ii = 0; ii < weights.Length; ii++)
        {
          sum += weights[ii] * comPoints.Values.ElementAt(ii).ElementAt(i).Last.Value;
        }

        return new BarShape { X = i, Y = sum } as IShape;

      }).ToList();

      var composer = new Composer
      {
        Name = "Demo",
        Items = chartPoints
      };

      await view.Create<CanvasEngine>(() => composer);
      await view.Update();

      Console.WriteLine(string.Join(Environment.NewLine, items.Keys.Select((o, i) => $"{o}: {weights[i]}")));
    }

    /// <summary>
    /// Linear weights
    /// </summary>
    /// <param name="points"></param>
    /// <param name="modelPoints"></param>
    /// <returns></returns>
    protected double[] GetLinearWeights(IDictionary<string, List<PointModel>> points, IList<double> modelPoints)
    {
      var matrix = new double[modelPoints.Count, points.Count];

      for (var i = 0; i < points.Count; i++)
      {
        for (var ii = 0; ii < modelPoints.Count; ii++)
        {
          matrix[ii, i] = points.Values.ElementAt(i).ElementAt(ii).Last.Value;
        }
      }

      lsfitlinear([.. modelPoints], matrix, out int info, out double[] weights, out var report);

      return weights;
    }

    /// <summary>
    /// Non-linear weights
    /// </summary>
    /// <param name="points"></param>
    /// <param name="modelPoints"></param>
    /// <returns></returns>
    protected double[] GetNonLinearWeights(IDictionary<string, List<PointModel>> points, IList<double> modelPoints)
    {
      var count = modelPoints.Count;
      var matrix = new double[count, points.Count];

      for (var i = 0; i < points.Count; i++)
      {
        for (var ii = 0; ii < count; ii++)
        {
          matrix[ii, i] = points.Values.ElementAt(i).ElementAt(ii).Last.Value;
        }
      }

      static void modelFunction(double[] c, double[] x, ref double action, object v)
      {
        action = c.Select((o, i) => c.ElementAtOrDefault(i) * x.ElementAtOrDefault(i)).Sum() + c.LastOrDefault();
      }

      var epsx = 0.000001;

      lsfitcreatef(matrix, [.. modelPoints], points.Select(o => 1.0).ToArray(), epsx, out lsfitstate state);
      lsfitsetcond(state, epsx, 0);
      lsfitfit(state, modelFunction, null, null);
      lsfitresults(state, out int info, out var weights, out lsfitreport rep);

      return weights;
    }

    /// <summary>
    /// Genrate trend
    /// </summary>
    /// <param name="count"></param>
    /// <param name="slope"></param>
    /// <returns></returns>
    protected List<double> GetSlope(double count, double slope)
    {
      List<double> response = [];

      for (var i = 0; i < count; i++)
      {
        response.Add(response.ElementAtOrDefault(i - 1) + slope);
      }

      return response;
    }

    /// <summary>
    /// Generate sin wave
    /// </summary>
    /// <param name="count"></param>
    /// <param name="waves"></param>
    /// <param name="amplitude"></param>
    /// <param name="frequency"></param>
    /// <param name="phase"></param>
    /// <returns></returns>
    protected List<double> GetSpread(double count, double waves, double amplitude, double frequency, double phase)
    {
      List<double> response = [];

      var xMin = 0.0;
      var xMax = 2 * Math.PI * waves;
      var step = (xMax - xMin) / count;

      for (var i = xMin; i <= xMax; i += step)
      {
        response.Add(amplitude * Math.Sin(frequency * i + phase));
      }

      return response.Take((int)count).ToList();
    }

    /// <summary>
    /// Sync points by date
    /// </summary>
    /// <param name="points"></param>
    /// <returns></returns>
    protected IDictionary<string, List<PointModel>> GetComparablePoints(IDictionary<string, IList<PointModel>> points)
    {
      var service = new AverageService();
      var response = points.ToDictionary(o => o.Key, o => new List<double>());
      var averages = points.ToDictionary(o => o.Key, o => new List<PointModel>());
      var stampMap = points.ToDictionary(o => o.Key, o => o.Value.GroupBy(o => o.Time).ToDictionary(o => o.Key, o => o.First()));
      var stamps = points
        .Values
        .SelectMany(series => series.Select(item => item.Time))
        .Distinct()
        .OrderBy(o => o)
        .ToList();

      foreach (var name in points.Keys)
      {
        var index = 0;
        var interval = 10;

        foreach (var stamp in stamps)
        {
          var asset = stampMap.TryGetValue(name, out var stampItem);
          var current = (stampItem.TryGetValue(stamp, out var o) ? o : new PointModel { Last = 0 }).Clone() as PointModel;
          response[name].Add(current.Last.Value);

          var average = service.SimpleAverage(response[name], index, interval);
          current.Last = index < interval ? current.Last : average;
          averages[name].Add(current);
          index++;
        }
      }

      return averages;
    }
  }
}
