using Canvas.Core.Engines;
using Canvas.Core.Models;
using Canvas.Core.Shapes;
using Canvas.Views.Web.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Core.Extensions;
using Terminal.Core.Models;

namespace Client.Components
{
  public partial class ChartsComponent : IDisposable
  {
    /// <summary>
    /// Reference to view control
    /// </summary>
    protected virtual CanvasGroupView View { get; set; }

    /// <summary>
    /// Points
    /// </summary>
    protected virtual IList<IShape> Shapes { get; set; } = [];

    /// <summary>
    /// Series
    /// </summary>
    protected virtual IDictionary<string, string> Maps { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Indices
    /// </summary>
    protected virtual IDictionary<long, IShape> Indices { get; set; } = new Dictionary<long, IShape>();

    /// <summary>
    /// Define chart model
    /// </summary>
    /// <param name="group"></param>
    public virtual async Task Create(IShape group)
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
    public virtual Task UpdateItems(IList<KeyValuePair<string, PointModel>> inputs, int? count = null)
    {
      foreach (var input in inputs)
      {
        var inputValue = input.Value;
        var index = inputValue.Time.Value.Ticks;
        var areaKey = Maps.Get(input.Key);

        if (areaKey is null)
        {
          continue;
        }

        if (Indices.TryGetValue(index, out IShape currentPoint) is false)
        {
          currentPoint = (Shapes.LastOrDefault()?.Clone() ?? View.Item.Clone()) as IShape;
          currentPoint.X = index;

          Shapes.Add(currentPoint);
          Indices[index] = currentPoint;
        }

        var series = currentPoint?.Groups?.Get(areaKey)?.Groups?.Get(input.Key);

        if (series is not null)
        {
          var currentBar = series as CandleShape;

          series.Y = inputValue?.Last ?? 0;

          if (currentBar is not null)
          {
            currentBar.L = inputValue?.Bar?.Low;
            currentBar.H = inputValue?.Bar?.High;
            currentBar.O = inputValue?.Bar?.Open;
            currentBar.C = inputValue?.Bar?.Close;
          }
        }
      }

      var domain = new DomainModel
      {
        IndexDomain = [Shapes.Count - (count ?? Shapes.Count), Shapes.Count]
      };

      return Render(() => View.Update(domain, Shapes));
    }

    /// <summary>
    /// Clear points
    /// </summary>
    public virtual void Clear()
    {
      Shapes.Clear();
      Indices.Clear();
      Render(() => View.Update(new DomainModel { IndexDomain = [0, 0] }, Shapes), false);
    }

    /// <summary>
    /// Dispose
    /// </summary>
    public virtual void Dispose() => View?.Dispose();
  }
}
