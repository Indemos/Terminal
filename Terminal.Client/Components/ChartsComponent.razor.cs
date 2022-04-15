using Canvas.Core;
using Canvas.Core.EngineSpace;
using Canvas.Views.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Core.MessageSpace;
using Terminal.Core.ModelSpace;
using Model = Canvas.Core.ModelSpace.Model;
using GroupModel = Canvas.Core.ModelSpace.GroupModel;
using IGroupModel = Canvas.Core.ModelSpace.IGroupModel;
using ICanvasModel = Canvas.Core.ModelSpace.IPointModel;

namespace Terminal.Client.Components
{
  public partial class ChartsComponent : IDisposable
  {
    /// <summary>
    /// Points
    /// </summary>
    protected IList<ICanvasModel> Points { get; set; } = new List<ICanvasModel>();

    /// <summary>
    /// Cache
    /// </summary>
    protected IDictionary<long, IGroupModel> Indices { get; set; } = new Dictionary<long, IGroupModel>();

    /// <summary>
    /// Views
    /// </summary>
    protected IDictionary<string, CanvasWebView> Views { get; set; } = new Dictionary<string, CanvasWebView>();

    /// <summary>
    /// Chart structure
    /// </summary>
    public IDictionary<string, IDictionary<string, IGroupModel>> Groups { get; set; } = new Dictionary<string, IDictionary<string, IGroupModel>>();

    /// <summary>
    /// Dispose
    /// </summary>
    public void Dispose()
    {
    }

    /// <summary>
    /// Dispose
    /// </summary>
    public void OnConnect()
    {
      Points.Clear();
    }

    /// <summary>
    /// Quote processor
    /// </summary>
    /// <param name="message"></param>
    public void OnData(ITransactionMessage<IPointModel> message)
    {
      var point = message.Next;
      var index = point.Time.Value.Ticks;

      if (Indices.TryGetValue(index, out IGroupModel canvasModel) is false)
      {
        canvasModel = new GroupModel
        {
          Index = index,
          Groups = new Dictionary<string, IGroupModel>()
        };

        Points.Add(canvasModel);
        Indices.Add(index, canvasModel);
      }

      foreach (var viewGroup in Groups)
      {
        if (canvasModel.Groups.TryGetValue(viewGroup.Key, out IGroupModel area) is false)
        {
          area = canvasModel.Groups[viewGroup.Key] = new GroupModel();
        }

        foreach (var seriesGroup in viewGroup.Value)
        {
          if (area.Groups.TryGetValue(seriesGroup.Key, out IGroupModel series) is false)
          {
            series = area.Groups[seriesGroup.Key] = seriesGroup.Value.Clone() as IGroupModel;
          }

          if (seriesGroup.Key.Equals(point.Name))
          {
            series.Value = new Model { ["Point"] = point.Last };
          }
        }

        var view = Views[viewGroup.Key];
        var composer = view.Composer as GroupComposer;

        composer.Points = Points;
        composer.IndexDomain ??= new int[2];
        composer.IndexDomain[0] = composer.Points.Count - composer.IndexCount;
        composer.IndexDomain[1] = composer.Points.Count;

        view.Update();
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

        var sources = new List<TaskCompletionSource>();

        foreach (var view in Views)
        {
          sources.Add(new TaskCompletionSource());
          view.Value.OnSize = view.Value.OnCreate = message => OnCreate(view.Key, message, sources.Last());
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

        await Task.WhenAll(sources.Select(o => o.Task));
      }

      await base.OnAfterRenderAsync(setup);
    }

    /// <summary>
    /// On load event for web view
    /// </summary>
    /// <param name="name"></param>
    /// <param name="message"></param>
    /// <param name="source"></param>
    /// <returns></returns>
    protected void OnCreate(string name, ViewMessage message, TaskCompletionSource source)
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

      source.TrySetResult();
    }
  }
}
