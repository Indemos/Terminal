using Canvas.Core.Composers;
using Canvas.Core.Engines;
using Canvas.Core.Enums;
using Canvas.Core.Models;
using Canvas.Core.Services;
using Canvas.Core.Shapes;
using Canvas.Views.Web.Views;
using Distribution.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using MudBlazor;
using Schwab;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Extensions;
using Terminal.Core.Models;

namespace Terminal.Pages.Utils
{
  public partial class Options
  {
    [Inject] ISnackbar Snackbar { get; set; }
    [Inject] IConfiguration Configuration { get; set; }

    protected bool IsConnected { get; set; }
    protected List<IDisposable> Subscriptions { get; set; } = [];
    protected Dictionary<string, Dictionary<string, CanvasView>> Groups { get; set; } = [];

    /// <summary>
    /// Page setup
    /// </summary>
    /// <param name="setup"></param>
    /// <returns></returns>
    protected override async Task OnAfterRenderAsync(bool setup)
    {
      if (setup)
      {
        var name = "GME";

        await OnChart(name, DateTime.Now, DateTime.Now.AddDays(5), async (options) =>
        {
          await Group(name, true, options, async (view, records) =>
          {
            await ShowBars(
              "Volume",
              "OpenInterest",
              view,
              records);
          });
        });
      }

      await base.OnAfterRenderAsync(setup);
    }

    /// <summary>
    /// API query to get options by criteria
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="action"></param>
    /// <returns></returns>
    protected async Task OnChart(string name, DateTime start, DateTime end, Action<IList<InstrumentModel>> action)
    {
      var interval = new Timer(TimeSpan.FromSeconds(10));
      var scheduler = InstanceService<ScheduleService>.Instance;
      var options = await GetOptions(name, start, end);

      action(options);

      interval.Enabled = true;
      interval.AutoReset = false;
      interval.Elapsed += (sender, e) => scheduler.Send(async () =>
      {
        var options = await GetOptions(name, start, end);
        action(options);
        interval.Enabled = true;
      });

      Subscriptions.Add(interval);
    }

    /// <summary>
    /// Group by expirations 
    /// </summary>
    /// <param name="caption"></param>
    /// <param name="combine"></param>
    /// <param name="options"></param>
    /// <param name="action"></param>
    /// <returns></returns>
    public async Task Group(
      string caption,
      bool combine,
      IList<InstrumentModel> options,
      Func<CanvasView, IList<InstrumentModel>, Task> action)
    {
      if (Groups.ContainsKey(caption) is false)
      {
        Groups[caption] = new Dictionary<string, CanvasView> { [caption] = null };
        await InvokeAsync(StateHasChanged);
      }

      if (combine)
      {
        await Task.WhenAll(Groups[caption].Select(async o => await action(o.Value, options)));
        return;
      }

      var groups = options
        .GroupBy(o => $"{o.Derivative.ExpirationDate}", o => o)
        .ToDictionary(o => o.Key, o => o.ToList());

      Groups[caption] = Enumerable
        .Range(0, groups.Count)
        .ToDictionary(o => groups.Keys.ElementAt(o), o => null as CanvasView);

      await InvokeAsync(StateHasChanged);
      await Task.WhenAll(Groups[caption].Select(async o => await action(o.Value, groups[o.Key])));
    }

    /// <summary>
    /// Show bar charts
    /// </summary>
    /// <param name="expressionUp"></param>
    /// <param name="expressionDown"></param>
    /// <param name="view"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    protected async Task ShowBars(
      string expressionUp,
      string expressionDown,
      CanvasView view,
      IList<InstrumentModel> options)
    {
      try
      {
        var groups = options
          .OrderBy(o => o.Derivative.Strike)
          .GroupBy(o => o.Derivative.Strike, o => o)
          .ToList();

        var comUp = new ComponentModel { Color = SKColors.DeepSkyBlue };
        var comDown = new ComponentModel { Color = SKColors.OrangeRed };
        var points = groups.Select((group, i) =>
        {
          var ups = Compute(expressionUp, [.. group]);
          var downs = -Compute(expressionDown, [.. group]);

          return new Shape
          {
            X = i,
            Groups = new Dictionary<string, IShape>
            {
              ["Indicators"] = new Shape
              {
                Groups = new Dictionary<string, IShape>
                {
                  ["Ups"] = new BarShape { Y = ups, Component = comUp },
                  ["Downs"] = new BarShape { Y = downs, Component = comDown }
                }
              }
            }
          } as IShape;

        }).ToList();

        string showIndex(double o)
        {
          var index = Math.Max(Math.Min((int)Math.Round(o), groups.Count - 1), 0);
          var group = groups.ElementAtOrDefault(index) ?? default;
          var price = group?.ElementAtOrDefault(0)?.Derivative?.Strike;

          return price is null ? null : $"{price}";
        }

        var composer = new GroupComposer
        {
          Name = "Indicators",
          Items = points,
          ShowIndex = showIndex
        };

        if (view is not null)
        {
          await view.Create<CanvasEngine>(() => composer);
          await view.Update();
        }
      }
      catch (Exception e)
      {
        Snackbar.Add("Invalid expression: " + e.Message);
      }
    }

    /// <summary>
    /// Show balance charts
    /// </summary>
    /// <param name="expressionUp"></param>
    /// <param name="expressionDown"></param>
    /// <param name="view"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    protected async Task ShowBalance(
      string expressionUp,
      string expressionDown,
      double price,
      CanvasView view,
      IList<InstrumentModel> options)
    {
      try
      {
        if (price is 0)
        {
          price = options.FirstOrDefault()?.Basis?.Point?.Last ?? 0;
        }

        var groups = options
          .OrderBy(o => o.Derivative.Strike)
          .GroupBy(o => o.Derivative.Strike, o => o)
          .ToList();

        var index = groups
          .Select((o, i) => new { Index = i, Data = o })
          .FirstOrDefault(o => o.Data.Key >= price)
          ?.Index ?? 0;

        if (index is 0)
        {
          index = groups.Count / 2;
        }

        var indexUp = index;
        var indexDown = index;
        var sumUp = 0.0;
        var sumDown = 0.0;
        var points = new IShape[groups.Count];
        var comUp = new ComponentModel { Color = SKColors.DeepSkyBlue };
        var comDown = new ComponentModel { Color = SKColors.OrangeRed };

        for (var i = 0; i < groups.Count; i++)
        {
          if (indexUp < groups.Count)
          {
            var sum = Compute(expressionUp, [.. groups.ElementAtOrDefault(indexUp)]);

            sumUp += sum;
            points[indexUp] = new Shape();
            points[indexUp].Groups = new Dictionary<string, IShape>();
            points[indexUp].Groups["Indicators"] = new Shape();
            points[indexUp].Groups["Indicators"].Groups = new Dictionary<string, IShape>();
            points[indexUp].Groups["Indicators"].Groups["Ups"] = new AreaShape { Y = sumUp, Component = comUp };
          }

          if (indexDown >= 0)
          {
            var sum = Compute(expressionDown, [.. groups.ElementAtOrDefault(indexDown)]);

            sumDown += sum;
            points[indexDown] = new Shape();
            points[indexDown].Groups = new Dictionary<string, IShape>();
            points[indexDown].Groups["Indicators"] = new Shape();
            points[indexDown].Groups["Indicators"].Groups = new Dictionary<string, IShape>();
            points[indexDown].Groups["Indicators"].Groups["Downs"] = new AreaShape { Y = sumDown, Component = comDown };
          }

          indexUp++;
          indexDown--;
        }

        string showIndex(double o)
        {
          var index = Math.Max(Math.Min((int)Math.Round(o), groups.Count - 1), 0);
          var group = groups.ElementAtOrDefault(index) ?? default;
          var price = group.ElementAtOrDefault(0)?.Derivative?.Strike;

          return price is null ? null : $"{price}";
        }

        var composer = new GroupComposer
        {
          Name = "Indicators",
          Items = points,
          ShowIndex = showIndex
        };

        await view.Create<CanvasEngine>(() => composer);
        await view.Update();
      }
      catch (Exception e)
      {
        Snackbar.Add("Invalid expression: " + e.Message);
      }
    }

    /// <summary>
    /// Show map chart
    /// </summary>
    /// <param name="expression"></param>
    /// <param name="view"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    protected async Task ShowMaps(string expression, CanvasView view, IList<InstrumentModel> options)
    {
      try
      {
        var groups = options
          .OrderBy(o => o.Derivative.Strike)
          .GroupBy(o => o.Derivative.Strike, o => o)
          .ToDictionary(o => o.Key.Value, o => o
            .OrderBy(option => option.Derivative.ExpirationDate)
            .GroupBy(option => option.Derivative.ExpirationDate)
            .ToDictionary(group => group.Key.Value,
             group => Compute(expression, [.. group])));

        var min = groups.Min(o => o.Value.Values.Min());
        var max = groups.Max(o => o.Value.Values.Max());
        var colorService = new ColorService { Min = min, Max = max, Mode = ShadeEnum.Intensity };
        var expirationMap = new Dictionary<string, DateTime>();
        var points = groups.Select(group =>
        {
          return new ColorMapShape
          {
            Points = group.Value.Select(date =>
            {
              expirationMap[$"{date.Key}"] = date.Key;

              return new ComponentModel
              {
                Size = date.Value,
                Color = colorService.GetColor(date.Value)
              } as ComponentModel?;

            }).ToList()
          };
        }).ToArray();

        var expirations = expirationMap
          .Values
          .OrderBy(o => o)
          .Select(o => $"{o:yyyy-MM-dd}")
          .ToList();

        string showIndex(double o)
        {
          var index = Math.Min(Math.Max((int)o, 0), points.Length - 1);
          var name = groups.Keys.ElementAtOrDefault(index);

          return $"{name}";
        }

        string showValue(double o)
        {
          var index = Math.Min(Math.Max((int)o, 0), expirations.Count - 1);
          var caption = expirations.ElementAtOrDefault(index);

          return $"{caption}";
        }

        var composer = new MapComposer
        {
          Name = "Indicators",
          Items = points,
          Range = points.Max(o => o.Points.Count),
          Values = Math.Min(expirations.Count, 5),
          ShowIndex = showIndex,
          ShowValue = showValue
        };

        await view.Create<CanvasEngine>(() => composer);
        await view.Update();
      }
      catch (Exception e)
      {
        Snackbar.Add("Invalid expression: " + e.Message);
      }
    }

    /// <summary>
    /// Evaluate expression for the option
    /// </summary>
    /// <param name="expression"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    private double Compute(string expression, IList<InstrumentModel> options)
    {
      var ins = new InstrumentModel();
      var vars = new Dictionary<string, double>();
      var engine = new CalcEngine.Core.CalculationEngine();

      void compute(string side, InstrumentModel option)
      {
        var x = option.Point;
        var o = option.Derivative;
        var v = o.Variance;

        vars[$"{side}{nameof(v.Vega)}"] = vars.Get($"{side}{nameof(v.Vega)}") + v.Vega.Value;
        vars[$"{side}{nameof(v.Gamma)}"] = vars.Get($"{side}{nameof(v.Gamma)}") + v.Gamma.Value;
        vars[$"{side}{nameof(v.Theta)}"] = vars.Get($"{side}{nameof(v.Theta)}") + v.Theta.Value;
        vars[$"{side}{nameof(v.Delta)}"] = vars.Get($"{side}{nameof(v.Delta)}") + v.Delta.Value;
        vars[$"{side}{nameof(x.BidSize)}"] = vars.Get($"{side}{nameof(x.BidSize)}") + x.BidSize.Value;
        vars[$"{side}{nameof(x.AskSize)}"] = vars.Get($"{side}{nameof(x.AskSize)}") + x.AskSize.Value;
        vars[$"{side}{nameof(x.Volume)}"] = vars.Get($"{side}{nameof(x.Volume)}") + x.Volume.Value;
        vars[$"{side}{nameof(o.Volatility)}"] = vars.Get($"{side}{nameof(o.Volatility)}") + o.Volatility.Value;
        vars[$"{side}{nameof(o.OpenInterest)}"] = vars.Get($"{side}{nameof(o.OpenInterest)}") + o.OpenInterest.Value;
        vars[$"{side}{nameof(o.IntrinsicValue)}"] = vars.Get($"{side}{nameof(o.IntrinsicValue)}") + o.IntrinsicValue.Value;
      }

      try
      {
        foreach (var option in options)
        {
          compute("", option);

          switch (option.Derivative.Side)
          {
            case OptionSideEnum.Put: compute("P", option); break;
            case OptionSideEnum.Call: compute("C", option); break;
          }
        }

        foreach (var v in vars)
        {
          engine.Variables[v.Key] = v.Value;
        }

        return Convert.ToDouble(engine.Evaluate(expression));
      }
      catch (Exception e)
      {
        Snackbar.Add("Invalid expression: " + e.Message);
      }

      return 0;
    }

    public async Task<IList<InstrumentModel>> GetOptions(string name, DateTime start, DateTime end)
    {
      var account = new Account
      {
        Descriptor = Configuration.GetValue<string>("Schwab:Account")
      };

      var adapter = new Adapter
      {
        Account = account,
        AccessToken = Configuration["Schwab:AccessToken"],
        RefreshToken = Configuration["Schwab:RefreshToken"],
        ClientId = Configuration["Schwab:ConsumerKey"],
        ClientSecret = Configuration["Schwab:ConsumerSecret"]
      };

      var interval = new Timer(TimeSpan.FromMinutes(1));
      var scheduler = InstanceService<ScheduleService>.Instance;

      if (IsConnected is false)
      {
        await adapter.Connect();
        IsConnected = true;
      }

      var options = await adapter.GetOptions(new ConditionModel
      {
        ["symbol"] = name,
        ["strikeCount"] = 50,
        ["fromDate"] = $"{start:yyyy-MM-dd}",
        ["toDate"] = $"{end.AddYears(1):yyyy-MM-dd}"
      });

      return options?.Data ?? [];
    }
  }
}
