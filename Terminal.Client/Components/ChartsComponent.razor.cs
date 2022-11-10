using Canvas.Core.EngineSpace;
using Canvas.Core.ModelSpace;
using Canvas.Core.ShapeSpace;
using Canvas.Views.Web.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Core.ExtensionSpace;
using Terminal.Core.ModelSpace;

namespace Terminal.Client.Components
{
  public partial class ChartsComponent : IDisposable, IAsyncDisposable
  {
    /// <summary>
    /// Reference to view control
    /// </summary>
    protected CanvasGroupView View { get; set; }

    /// <summary>
    /// Points
    /// </summary>
    protected IList<IShape> Shapes { get; set; } = new List<IShape>();

    /// <summary>
    /// Series
    /// </summary>
    protected IDictionary<string, string> Maps { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Indices
    /// </summary>
    protected IDictionary<long, IGroupShape> Indices { get; set; } = new Dictionary<long, IGroupShape>();

    /// <summary>
    /// Define chart model
    /// </summary>
    /// <param name="group"></param>
    public async Task Create(IGroupShape group)
    {
      (View.Item = group)
        .Groups
        .ForEach(view => view.Value.Groups
        .ForEach(series => Maps[series.Key] = view.Key));

      await View.CreateViews<CanvasEngine>();
    }

    /// <summary>
    /// Update
    /// </summary>
    /// <param name="inputs"></param>
    /// <param name="count"></param>
    public void UpdateItems(IList<IPointModel> inputs, int count)
    {
      foreach (var input in inputs)
      {
        var index = input.Time.Value.Ticks;
        var previousPoint = (Shapes.ElementAtOrDefault(Shapes.Count - 2) ?? View.Item.Clone()) as IGroupShape;

        if (Indices.TryGetValue(index, out IGroupShape currentPoint) is false)
        {
          currentPoint = previousPoint.Clone() as IGroupShape;
          currentPoint.X = index;

          Shapes.Add(currentPoint);
          Indices[index] = currentPoint;
        }

        var series = currentPoint?.Groups?.Get(Maps[input.Name])?.Groups?.Get(input.Name);

        if (series is not null)
        {
          var currentBar = series as CandleShape;

          series.Y = input?.Last ?? 0;

          if (currentBar is not null)
          {
            currentBar.L = input?.Group?.Low;
            currentBar.H = input?.Group?.High;
            currentBar.O = input?.Group?.Open;
            currentBar.C = input?.Group?.Close;
          }
        }
      }

      var domain = new DomainModel
      {
        IndexDomain = new[] { Shapes.Count - count, Shapes.Count }
      };

      View.Update(domain, Shapes);
    }

    /// <summary>
    /// Clear points
    /// </summary>
    public void Clear()
    {
      Shapes.Clear();
      Indices.Clear();
    }

    /// <summary>
    /// Dispose
    /// </summary>
    /// <returns></returns>
    public ValueTask DisposeAsync()
    {
      Dispose();

      return new ValueTask(Task.CompletedTask);
    }

    /// <summary>
    /// Dispose
    /// </summary>
    public void Dispose()
    {
      View?.Dispose();
    }
  }
}
