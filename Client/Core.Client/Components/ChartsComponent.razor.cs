using Canvas.Core.Composers;
using Canvas.Core.Engines;
using Canvas.Core.Models;
using Canvas.Core.Shapes;
using Canvas.Views.Web.Views;
using Core.Client.Services;
using Core.Common.Enums;
using Core.Common.Extensions;
using Core.Common.Models;
using Microsoft.AspNetCore.Components;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Client.Components
{
  public partial class ChartsComponent : IDisposable
  {
    [Inject] public virtual MessageService Messenger { get; set; }

    [Parameter] public virtual string Name { get; set; }

    /// <summary>
    /// Upside style
    /// </summary>
    protected virtual ComponentModel UpSide { get; set; }

    /// <summary>
    /// Downside style
    /// </summary>
    protected virtual ComponentModel DownSide { get; set; }

    /// <summary>
    /// Indices
    /// </summary>
    protected virtual IDictionary<long, IShape> Indices { get; set; } = new ConcurrentDictionary<long, IShape>();

    /// <summary>
    /// Reference to view control
    /// </summary>
    public virtual CanvasGroupView View { get; set; }

    /// <summary>
    /// Points
    /// </summary>
    public virtual IList<IShape> Shapes { get; protected set; } = [];

    /// <summary>
    /// Labels
    /// </summary>
    public virtual IList<IComposer> Composers { get; set; } = [];

    /// <summary>
    /// Setup views
    /// </summary>
    /// <param name="setup"></param>
    protected override async Task OnAfterRenderAsync(bool setup)
    {
      await base.OnAfterRenderAsync(setup);

      if (setup)
      {
        Messenger.OnMessage = state =>
        {
          if (state.Previous is SubscriptionEnum.Progress && state.Next is SubscriptionEnum.None)
          {
            Clear();
          }
        };
      }
    }

    /// <summary>
    /// Define chart model
    /// </summary>
    /// <param name="group"></param>
    public virtual async Task Create(params string[] areas)
    {
      UpSide = new ComponentModel { Color = SKColors.DeepSkyBlue };
      DownSide = new ComponentModel { Color = SKColors.OrangeRed };
      View.Item = new Shape { Groups = areas.ToDictionary(o => o, o => new Shape() as IShape).Concurrent() };

      Composers = await View.CreateViews<CanvasEngine>();
    }

    /// <summary>
    /// Value to shape
    /// </summary>
    /// <param name="point"></param>
    public virtual IShape GetShape<T>(double? value, SKColor? color = null) where T : IShape, new()
    {
      return GetShape<T>(new PriceModel { Last = value }, color);
    }

    /// <summary>
    /// Point to shape
    /// </summary>
    /// <param name="point"></param>
    public virtual IShape GetShape<T>(PriceModel point, SKColor? color = null) where T : IShape, new()
    {
      if (typeof(T).Equals(typeof(CandleShape)))
      {
        return new CandleShape
        {
          L = point.Bar.Low,
          H = point.Bar.High,
          O = point.Bar.Open,
          C = point.Bar.Close,
          Component = point.Bar.Close > point.Bar.Open ? UpSide : DownSide
        };
      }

      return new T
      {
        Y = point.Last,
        Component = new ComponentModel { Color = color ?? SKColors.LimeGreen }
      };
    }

    /// <summary>
    /// Update
    /// </summary>
    /// <param name="index"></param>
    /// <param name="inputs"></param>
    public virtual async void UpdateItems(long index, string area, string series, params IShape[] inputs)
    {
      if (Messenger.State.Next is SubscriptionEnum.None)
      {
        return;
      }

      foreach (var input in inputs)
      {
        if (Indices.TryGetValue(index, out IShape currentPoint) is false)
        {
          currentPoint = new Shape { X = index };

          Shapes.Add(currentPoint);
          Indices[index] = currentPoint;
        }

        currentPoint.Groups[area] = currentPoint.Groups.Get(area) ?? new Shape();
        currentPoint.Groups[area].Groups[series] = input;
      }

      var domain = new DimensionModel
      {
        IndexDomain = [Shapes.Count - Math.Max(10, Shapes.Count), Shapes.Count]
      };

      await View.Update(domain, Shapes);
    }

    /// <summary>
    /// Update
    /// </summary>
    /// <param name="inputs"></param>
    public virtual void UpdateOrdinals(IList<IShape> shapes)
    {
      if (Messenger.State.Next is SubscriptionEnum.None)
      {
        return;
      }

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
