using Canvas.Core;
using Canvas.Core.EngineSpace;
using Canvas.Views.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Core.ExtensionSpace;
using Terminal.Core.ModelSpace;
using GroupModel = Canvas.Core.ModelSpace.GroupModel;
using ICanvasModel = Canvas.Core.ModelSpace.IPointModel;
using IGroupModel = Canvas.Core.ModelSpace.IGroupModel;
using Model = Canvas.Core.ModelSpace.Model;

namespace Terminal.Client.Components
{
  public partial class ChartsComponent : IDisposable
  {
    /// <summary>
    /// Points
    /// </summary>
    public IList<ICanvasModel> Points { get; set; } = new List<ICanvasModel>();

    /// <summary>
    /// Cache
    /// </summary>
    public IDictionary<long, IGroupModel> Indices { get; set; } = new Dictionary<long, IGroupModel>();

    /// <summary>
    /// Views
    /// </summary>
    public IDictionary<string, CanvasWebView> Views { get; set; } = new Dictionary<string, CanvasWebView>();

    /// <summary>
    /// Chart cache
    /// </summary>
    protected IDictionary<string, string> Cache { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Chart model
    /// </summary>
    protected IGroupModel Map { get; set; } = new GroupModel { Groups = new Dictionary<string, IGroupModel>() };

    /// <summary>
    /// Dispose
    /// </summary>
    public void Dispose()
    {
    }

    /// <summary>
    /// Define chart model
    /// </summary>
    /// <param name="map"></param>
    public void CreateMap(IGroupModel map)
    {
      (Map = map)
        .Groups
        .ForEach(view => view.Value.Groups
        .ForEach(series => Cache[series.Key] = view.Key));
    }

    /// <summary>
    /// Render
    /// </summary>
    /// <param name="updates"></param>
    public void Update()
    {
      foreach (var view in Views)
      {
        var composer = view.Value.Composer;

        composer.Points = Points;
        composer.IndexDomain ??= new int[2];
        composer.IndexDomain[0] = composer.Points.Count - composer.IndexCount;
        composer.IndexDomain[1] = composer.Points.Count;

        view.Value.Update();
      }
    }

    /// <summary>
    /// Quote processor
    /// </summary>
    /// <param name="inputs"></param>
    public void OnData(ICollection<IPointModel> inputs)
    {
      foreach (var input in inputs)
      {
        var index = input.Time.Value.Ticks;

        if (Indices.TryGetValue(index, out IGroupModel original) is false)
        {
          var model = Map.Clone() as IGroupModel;

          model.Index = index;

          Points.Add(model);
          Indices.Add(index, model);
        }

        var currentPoint = Points.Last() as IGroupModel;
        var previousPoint = Points.ElementAtOrDefault(Points.Count - 2) as IGroupModel;

        foreach (var series in Cache)
        {
          var currentSeries = currentPoint?.Groups.Get(Cache[series.Key]).Groups.Get(series.Key);
          var previousSeries = previousPoint?.Groups.Get(Cache[series.Key]).Groups.Get(series.Key);

          dynamic value = new Model();

          value.Point = input?.Last;
          value.Low = input?.Group?.Low;
          value.High = input?.Group?.High;
          value.Open = input?.Group?.Open;
          value.Close = input?.Group?.Close;

          switch (series.Key.Equals(input.Name))
          {
            case true: currentSeries.Value = value; break;
            case false: currentSeries.Value ??= previousSeries?.Value?.Clone(); break;
          }
        }
      }
    }

    /// <summary>
    /// Load
    /// </summary>
    /// <param name="setup"></param>
    /// <returns></returns>
    protected override async Task OnAfterRenderAsync(bool setup)
    {
      if (setup)
      {
        await InvokeAsync(StateHasChanged);

        foreach (var view in Views)
        {
          view.Value.OnSize = view.Value.OnCreate = message => OnCreate(view.Key, message);
          view.Value.OnUpdate = message => Views.ForEach(o =>
          {
            if (Equals(o.Value.Composer.Name, message.View.Composer.Name) is false)
            {
              o.Value.Composer.IndexDomain = message.View.Composer.IndexDomain;
              o.Value.Update();
            }
          });

          await view.Value.Create();
        }
      }

      await base.OnAfterRenderAsync(setup);
    }

    /// <summary>
    /// On load event for web view
    /// </summary>
    /// <param name="name"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    protected void OnCreate(string name, ViewMessage message)
    {
      var composer = new GroupComposer
      {
        Name = name,
        Engine = new CanvasEngine(message.X, message.Y)
      };

      if (message?.View?.Composer is not null)
      {
        composer.Points = message.View.Composer.Points;
        composer.IndexDomain = message.View.Composer.IndexDomain;
        composer.ValueDomain = message.View.Composer.ValueDomain;
        message.View.Composer.Dispose();
      }

      Views[name].Composer = composer;
      Views[name].Update();
    }
  }
}
