using Core.Client.Services;
using Core.Common.Enums;
using Core.Common.Grains;
using Core.Common.Implementations;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Client.Components
{
  public partial class OrdersComponent
  {
    [Inject] public virtual SubscriptionService Observer { get; set; }

    [Parameter] public virtual string Name { get; set; }

    public struct Row
    {
      public string Name { get; set; }
      public string Group { get; set; }
      public OrderTypeEnum? Type { get; set; }
      public double Size { get; set; }
      public double Price { get; set; }
      public DateTime? Time { get; set; }
      public OrderSideEnum? Side { get; set; }
    }

    /// <summary>
    /// Sync
    /// </summary>
    protected Task Update { get; set; } = Task.CompletedTask;

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
        Observer.OnMessage += state =>
        {
          if (state.Previous is SubscriptionEnum.Progress && state.Next is SubscriptionEnum.None)
          {
            Clear();
          }
        };
      }
    }

    /// <summary>
    /// Update table records 
    /// </summary>
    /// <param name="adapters"></param>
    public virtual async Task UpdateItems(params IGateway[] adapters)
    {
      if (Update.IsCompleted)
      {
        var queries = adapters.Select(o => o.Orders(default));
        var responses = await Task.WhenAll(queries);
        var orders = responses
          .SelectMany(o => o.Data)
          .OrderBy(o => o.Operation.Time)
          .ToList();

        Items = [.. orders.Select(o => new Row
        {
          Name = o?.Operation?.Instrument?.Name,
          Type = o.Type,
          Time = new DateTime(o.Operation.Time ?? DateTime.MinValue.Ticks),
          Group = o?.Operation?.Instrument?.Basis?.Name ?? o?.Operation?.Instrument?.Name,
          Side = o.Side,
          Size = o.Amount ?? 0,
          Price = o.Price ?? 0,

        })];

        Update = Task.WhenAll([InvokeAsync(StateHasChanged), Task.Delay(100)]);
      }
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
