using Canvas.Core.Composers;
using Canvas.Core.Engines;
using Canvas.Core.Models;
using Canvas.Core.Shapes;
using Canvas.Views.Web.Views;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Core.Extensions;
using Terminal.Core.Models;

namespace Terminal.Components
{
  public partial class ChartsComponent : IDisposable
  {
    /// <summary>
    /// Upside style
    /// </summary>
    protected virtual ComponentModel UpSide { get; set; }

    /// <summary>
    /// Downside style
    /// </summary>
    protected virtual ComponentModel DownSide { get; set; }

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
    /// Reference to view control
    /// </summary>
    public virtual CanvasGroupView View { get; set; }

    /// <summary>
    /// Labels
    /// </summary>
    public virtual IList<IComposer> Composers { get; set; } = [];

    /// <summary>
    /// Define chart model
    /// </summary>
    /// <param name="group"></param>
    public virtual async Task Create(IShape group)
    {
      UpSide = new ComponentModel { Color = SKColors.DeepSkyBlue };
      DownSide = new ComponentModel { Color = SKColors.OrangeRed };

      (View.Item = group)
        .Groups
        .ForEach(view => view.Value.Groups
        .ForEach(series => Maps[series.Key] = view.Key));

      Composers = await View.CreateViews<CanvasEngine>();

      await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Update
    /// </summary>
    /// <param name="inputs"></param>
    /// <param name="count"></param>
    public virtual void UpdateItems(IList<KeyValuePair<string, PointModel>> inputs, int? count = null)
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
          currentPoint.X = Shapes.Count;

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
            currentBar.Component = currentBar.C > currentBar.O ? UpSide : DownSide;
          }
        }
      }

      var domain = new DimensionModel
      {
        IndexDomain = [Shapes.Count - (count ?? Shapes.Count), Shapes.Count]
      };

      View.Update(domain, Shapes);
    }


    /// <summary>
    /// Update
    /// </summary>
    /// <param name="inputs"></param>
    public virtual void UpdateItems(IList<IShape> shapes)
    {
      var domain = new DimensionModel
      {
        IndexDomain = [0, shapes.Count]
      };

      View.Update(domain, shapes);
    }

    /// <summary>
    /// Clear points
    /// </summary>
    public virtual void Clear()
    {
      Shapes.Clear();
      Indices.Clear();
      View.Update(new DimensionModel { IndexDomain = [0, 0] }, Shapes);
    }

    /// <summary>
    /// Dispose
    /// </summary>
    public virtual void Dispose() => View?.Dispose();
  }
}
