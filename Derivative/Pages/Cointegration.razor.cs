using Canvas.Core.Composers;
using Canvas.Core.Engines;
using Canvas.Core.Shapes;
using Canvas.Views.Web.Views;
using Derivative.Models;
using Derivative.Pages.Popups;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using MudBlazor;
using Schwab;
using Schwab.Messages;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Core.Domains;
using Terminal.Core.Extensions;
using Terminal.Core.Models;
using Terminal.Core.Services;
using static alglib;

namespace Derivative.Pages
{
  public partial class Cointegration
  {
    [Inject] ISnackbar Snackbar { get; set; }
    [Inject] IDialogService ModalService { get; set; }
    [Inject] IConfiguration Configuration { get; set; }

    protected int Count { get; set; } = 1;
    protected bool IsLoading { get; set; }
    protected Dictionary<string, Dictionary<string, CanvasView>> Groups { get; set; } = [];

    /// <summary>
    /// Clear
    /// </summary>
    public void OnClear()
    {
      Count = 1;
      Groups.Clear();
    }

    /// <summary>
    /// Bar chart editor
    /// </summary>
    /// <returns></returns>
    public async Task OnCombine()
    {
      await OnChart<PortfolioEditor>(async (caption, response, items) =>
      {
        if (Groups.ContainsKey(caption) is false)
        {
          Groups[caption] = new Dictionary<string, CanvasView> { [caption] = null };
          await InvokeAsync(StateHasChanged);
        }

        Groups[caption].ForEach(async o => await Show(o.Value, response, items));
      });
    }

    /// <summary>
    /// API query to get options by criteria
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="action"></param>
    /// <returns></returns>
    protected async Task OnChart<T>(Func<string, PortfolioInputModel, IDictionary<string, IList<PointModel>>, Task> action) where T : ComponentBase
    {
      var props = new DialogOptions
      {
        FullWidth = true,
        MaxWidth = MaxWidth.ExtraSmall,
        CloseOnEscapeKey = true
      };

      var response = await ModalService
        .ShowAsync<T>("Editor", props)
        .ContinueWith(async process =>
        {
          IsLoading = true;

          var popup = await process;
          var response = await popup.Result;

          if (response.Canceled is false)
          {
            var data = response.Data as PortfolioInputModel;
            var adapter = new Adapter
            {
              Account = new Account
              {
                Descriptor = Configuration.GetValue<string>("Schwab:Account")
              },
              Scope = new ScopeMessage
              {
                ConsumerKey = Configuration.GetValue<string>("Schwab:ConsumerKey"),
                ConsumerSecret = Configuration.GetValue<string>("Schwab:ConsumerSecret"),
                RefreshToken = Configuration.GetValue<string>("Schwab:RefreshToken"),
                AccessToken = Configuration.GetValue<string>("Schwab:AccessToken")
              }
            };

            var caption = $"{Count++} : {data.Names} : {data.Duration} : {data.Resolution}";

            await adapter.Connect();

            var names = data.Names.Split(",");
            var points = new Dictionary<string, IList<PointModel>>();

            foreach (var name in names)
            {
              var assetPoints = await adapter.GetPoints(new PointScreenerModel(), new Hashtable
              {
                ["period"] = 1,
                ["frequency"] = 1,
                ["symbol"] = name,
                ["periodType"] = data.Duration.ToLower(),
                ["frequencyType"] = data.Resolution.ToLower()
              });

              points[name] = assetPoints
                .Data
                .Where(o => o.Last is not null)
                .TakeLast((int)data.Count)
                .ToList();
            }

            await action(caption, data, points);
          }

          IsLoading = false;

          await InvokeAsync(StateHasChanged);
        });
    }

    /// <summary>
    /// Show bar charts
    /// </summary>
    /// <param name="view"></param>
    /// <param name="items"></param>
    /// <returns></returns>
    protected async Task Show(CanvasView view, PortfolioInputModel inputModel, IDictionary<string, IList<PointModel>> items)
    {
      var modelPoints = new List<double>();
      var comPoints = GetComparablePoints(items);
      var count = Math.Min(inputModel.Count, comPoints.Min(o => o.Value.Count));
      var matrix = Matrix<double>.Build.DenseOfColumns(items.Values.Select(o => o.Select(v => v.Last.Value)));

      Console.WriteLine("SPY, IVV, VOO : " + CointegrationService.Johansen(matrix));


      static Matrix<double> GenerateCointegratedData(int observations)
      {
        Random random = new Random();
        Vector<double> Y1 = Vector<double>.Build.Dense(observations, i => random.NextDouble());
        Vector<double> Y2 = Y1 + Vector<double>.Build.Dense(observations, i => random.NextDouble() * 0.1); // Linear relation + small noise
        Vector<double> Y3 = 2 * Y1 + Vector<double>.Build.Dense(observations, i => random.NextDouble() * 0.1); // Another linear relation + small noise

        Matrix<double> cointegratedMatrix = Matrix<double>.Build.Dense(observations, 3);
        cointegratedMatrix.SetColumn(0, Y1);
        cointegratedMatrix.SetColumn(1, Y2);
        cointegratedMatrix.SetColumn(2, Y3);

        return cointegratedMatrix;
      }

      // Non-Cointegrated Data Generator
      static Matrix<double> GenerateNonCointegratedRandomWalkData(int observations)
      {
        Random random = new Random();
        Vector<double> Y1 = Vector<double>.Build.Dense(observations, 0);
        Vector<double> Y2 = Vector<double>.Build.Dense(observations, 0);
        Vector<double> Y3 = Vector<double>.Build.Dense(observations, 0);

        for (int i = 1; i < observations; i++)
        {
          Y1[i] = Y1[i - 1] + random.NextDouble() - 0.5;  // Random walk
          Y2[i] = Y2[i - 1] + random.NextDouble() - 0.5;  // Random walk
          Y3[i] = Y3[i - 1] + random.NextDouble() - 0.5;  // Random walk
        }

        // Combine Y1, Y2, Y3 into a matrix
        Matrix<double> nonCointegratedMatrix = Matrix<double>.Build.Dense(observations, 3);
        nonCointegratedMatrix.SetColumn(0, Y1);
        nonCointegratedMatrix.SetColumn(1, Y2);
        nonCointegratedMatrix.SetColumn(2, Y3);

        return nonCointegratedMatrix;
      }

      Console.WriteLine("######## Cointegrated " + CointegrationService.Johansen(GenerateCointegratedData(100)));
      Console.WriteLine("######## Noncointegrated " + CointegrationService.Johansen(GenerateNonCointegratedRandomWalkData(100)));


      var composer = new Composer
      {
        Name = "Demo",
        Items = [],
        View = view
      };

      await view.Create<CanvasEngine>(() => composer);
      await view.Update();
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
