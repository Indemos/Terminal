using Distribution.Services;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Core.Enums;
using Terminal.Core.Models;
using Terminal.Services;

namespace Terminal.Components
{
  public partial class PositionsComponent
  {
    [Parameter] public virtual string Name { get; set; }

    public struct PositionRecord
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
    protected Task Update { get; set; } = Task.CompletedTask;

    /// <summary>
    /// Subscription state
    /// </summary>
    protected virtual SubscriptionService Subscription { get => InstanceService<SubscriptionService>.Instance; }

    /// <summary>
    /// Table records
    /// </summary>
    protected virtual IList<PositionRecord> Items { get; set; } = [];

    /// <summary>
    /// Setup views
    /// </summary>
    /// <param name="setup"></param>
    protected override async Task OnAfterRenderAsync(bool setup)
    {
      await base.OnAfterRenderAsync(setup);

      if (setup)
      {
        Subscription.OnUpdate += state =>
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
    /// <param name="items"></param>
    public virtual void UpdateItems(IEnumerable<OrderModel> items)
    {
      if (Subscription.State.Next is SubscriptionEnum.None)
      {
        return;
      }

      if (Update.IsCompleted)
      {
        Items = [.. items.Select(o => new PositionRecord
        {
          Name = o.Name,
          Group = o.BasisName ?? o.Name,
          Time = o.Transaction.Time,
          Side = o.Side ?? OrderSideEnum.None,
          Size = o.Transaction.Volume ?? 0,
          OpenPrice = o.Price ?? 0,
          ClosePrice = o.GetCloseEstimate() ?? 0,
          Gain = o.GetGainEstimate() ?? o.Gain ?? 0
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
