using Core.Client.Services;
using Core.Common.Enums;
using Core.Common.Grains;
using Core.Common.Services;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Core.Client.Components
{
  public partial class PositionsComponent
  {
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
    protected Task Update { get; set; } = Task.CompletedTask;

    /// <summary>
    /// Subscription state
    /// </summary>
    protected virtual SubscriptionService Subscription { get => InstanceService<SubscriptionService>.Instance; }

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
        Subscription.Update += state =>
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
    /// <param name="account"></param>
    public virtual void UpdateItems(params IGateway[] adapters)
    {
      // TODO: Get adapter and account from grain
      //if (Update.IsCompleted && Subscription.State.Next is not SubscriptionEnum.None)
      //{
      //  Items = adapters.SelectMany(adapter =>
      //  {
      //    var orders = adapter.Account.Positions.Values;
      //    var subOrders = orders.SelectMany(o => (adapter as Gateway).ComposeOrders(o));

      //    return subOrders;

      //  })
      //  .Select(o => new Row
      //  {
      //    Name = o.Name,
      //    Group = o.BasisName ?? o.Name,
      //    Time = o.Transaction.Time,
      //    Side = o.Side ?? OrderSideEnum.None,
      //    Size = o.Transaction.Amount ?? 0,
      //    OpenPrice = o.Transaction.AveragePrice ?? 0,
      //    ClosePrice = o.GetClosePrice() ?? 0,
      //    Gain = o.GetEstimate() ?? o.Gain ?? 0

      //  }).ToList();

      //  Update = Task.WhenAll([InvokeAsync(StateHasChanged), Task.Delay(100)]);
      //}
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
