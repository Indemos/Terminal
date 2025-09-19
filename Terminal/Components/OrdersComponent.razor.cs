using Distribution.Services;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Services;

namespace Terminal.Components
{
  public partial class OrdersComponent
  {
    [Inject] SubscriptionService Observer { get; set; }

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
        Observer.Update += state =>
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
    public virtual void UpdateItems(params IGateway[] adapters)
    {
      if (Update.IsCompleted && Observer.State.Next is not SubscriptionEnum.None)
      {
        Items = adapters.SelectMany(adapter =>
        {
          var orders = adapter.Account.Orders.Values;
          var positions = adapter.Account.Positions.Values;
          var subOrders = orders.SelectMany(o => (adapter as Gateway).ComposeOrders(o));
          var subPositions = positions.SelectMany(o => (adapter as Gateway).ComposeOrders(o));

          return subOrders.Concat(subPositions.SelectMany(o => o.Orders));

        })
        .Select(o => new Row
        {
          Name = o.Name,
          Type = o.Type,
          Time = o.Transaction.Time,
          Group = o.BasisName ?? o.Name,
          Side = o.Side ?? OrderSideEnum.None,
          Size = o.Amount ?? 0,
          Price = o.Price ?? 0,

        }).ToList();

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
