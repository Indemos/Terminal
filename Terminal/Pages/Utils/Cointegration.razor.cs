using Canvas.Core.Composers;
using Canvas.Core.Engines;
using Canvas.Core.Shapes;
using Canvas.Views.Web.Views;
using Estimator.Services;
using MathNet.Numerics.LinearAlgebra;
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
using Terminal.Core.Extensions;
using Terminal.Core.Models;

namespace Terminal.Pages.Utils
{
  public partial class Cointegration
  {
    int samples = 250;
    string[] names = ["SPY", "VXX"];

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
          var assetPoints = await adapter.GetPoints(new PointScreenerModel(), new Hashtable
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

        await Show(View, points, samples);
      }
    }

    /// <summary>
    /// Show bar charts
    /// </summary>
    /// <param name="view"></param>
    /// <param name="items"></param>
    /// <param name="itemsCount"></param>
    /// <returns></returns>
    protected async Task Show(CanvasView view, IDictionary<string, IList<PointModel>> items, int itemsCount)
    {
      var comPoints = GetComparablePoints(items);
      var count = Math.Min(itemsCount, comPoints.Min(o => o.Value.Count));
      var matrix = Matrix<double>.Build.DenseOfColumns(items.Values.Select(o => o.Select(v => v.Last.Value)));
      var (rank, eigenVectors) = CointegrationService.Johansen(matrix);
      var points = comPoints.Values.First().Select((item, index) =>
      {
        var sum = comPoints
          .Select((o, i) => o.Value.ElementAtOrDefault(index)?.Last * eigenVectors.ElementAtOrDefault(i))
          .Sum();

        return new BarShape { X = index, Y = sum } as IShape;

      }).ToList();

      var composer = new Composer
      {
        Name = "Demo",
        Items = points
      };

      await view.Create<CanvasEngine>(() => composer);
      await view.Update();

      Console.WriteLine(string.Join(Environment.NewLine, comPoints.Select((o, i) => $"{o.Key}: {eigenVectors[i]}")));
    }

    /// <summary>
    /// Sync points by date
    /// </summary>
    /// <param name="points"></param>
    /// <returns></returns>
    protected IDictionary<string, IList<PointModel>> GetComparablePoints(IDictionary<string, IList<PointModel>> points)
    {
      var count = points.Max(o => o.Value.Count);
      var indices = points.ToDictionary(o => o.Key, o => 0);
      var comPoints = new Dictionary<string, IList<PointModel>>();

      for (var i = 0; i < count; i++)
      {
        var stamp = points.Max(o => o.Value.ElementAtOrDefault(indices[o.Key])?.Time);

        foreach (var series in points)
        {
          var previous = points.Get(series.Key).ElementAtOrDefault(i - 1) ?? points.Get(series.Key).FirstOrDefault();
          var current = points.Get(series.Key).ElementAtOrDefault(i) ?? previous;

          comPoints[series.Key] = comPoints.Get(series.Key) ?? [];
          comPoints[series.Key].Add(current.Time <= stamp ? current : previous);
        }
      }

      return comPoints;
    }
  }
}
