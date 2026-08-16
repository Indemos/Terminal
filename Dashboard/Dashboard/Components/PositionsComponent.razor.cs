using Core.Conventions;
using Core.Enums;
using Core.Models;
using Core.Services;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dashboard.Components
{
  public partial class PositionsComponent
  {
    [Inject] public virtual StateService Observer { get; set; }

    [Parameter] public virtual string Name { get; set; }

    public struct Row
    {
      public string Name { get; set; }
      public string Group { get; set; }
      public double Size { get; set; }
      public double Gain { get; set; }
      public double OpenPrice { get; set; }
      public double ClosePrice { get; set; }
      public DateTime? Time { get; set; }
      public OrderSideEnum? Side { get; set; }
    }

    /// <summary>
    /// Sync
    /// </summary>
    protected Task Sync { get; set; } = Task.CompletedTask;

    /// <summary>
    /// Table records
    /// </summary>
    protected virtual IList<Row> Items { get; set; } = [];

    /// <summary>
    /// Setup views
    /// </summary>
    /// <param name="setup"></param>
    protected override async Task OnAfterRenderAsync(bool setup)
    {
      await base.OnAfterRenderAsync(setup);

      if (setup)
      {
        Observer.Update += state =>
        {
          if (state.Previous is SubscriptionEnum.Progress && state.Next is SubscriptionEnum.None)
          {
            Clear();
          }

          return Task.CompletedTask;
        };
      }
    }

    /// <summary>
    /// Update table records 
    /// </summary>
    /// <param name="adapters"></param>
    /// <param name="criteria"></param>
    public virtual async void Update(IEnumerable<IGateway> adapters, PositionCriteria criteria = default)
    {
      if (Sync.IsCompleted is false)
      {
        return;
      }

      var queries = adapters.Select(o => o.GetPositions(criteria));
      var responses = await Task.WhenAll(queries);
      var items = new List<Row>();

      foreach (var response in responses)
      {
        foreach (var o in response.Data)
        {
          items.Add(new()
          {
            Name = o?.Operation?.Instrument?.Name,
            Group = o?.Operation?.Instrument?.Basis?.Name ?? o?.Operation?.Instrument?.Name,
            Time = new DateTime(o.Operation.Time ?? DateTime.MinValue.Ticks),
            Side = o.Side,
            Size = o.Operation.Amount ?? 0,
            OpenPrice = o.Operation.AveragePrice ?? 0,
            ClosePrice = o?.Operation?.Instrument?.Price?.Last ?? 0,
            Gain = o.Balance.Current ?? 0
          });
        }
      }

      Items = items;

      await (Sync = Task.WhenAll(InvokeAsync(StateHasChanged), Task.Delay(TimeSpan.FromMilliseconds(100))));
    }

    /// <summary>
    /// Clear records
    /// </summary>
    public virtual void Clear()
    {
      Items = [];
      InvokeAsync(StateHasChanged);
    }
  }
}
