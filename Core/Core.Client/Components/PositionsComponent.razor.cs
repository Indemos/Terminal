using Core.Client.Services;
using Core.Common.Enums;
using Core.Common.Grains;
using Core.Common.Services;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
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
    public virtual async Task UpdateItems(params IGateway[] adapters)
    {
      if (Update.IsCompleted && Subscription.State.Next is not SubscriptionEnum.None)
      {
        var queries = adapters.Select(o => o.GetPositions());
        var responses = await Task.WhenAll(queries);
        var positions = responses
          .SelectMany(o => o.Data)
          .OrderBy(o => o.Operation.Time)
          .ToList();

        Items = [.. positions.Select(o => new Row
        {
          Name = o?.Operation?.Instrument?.Name,
          Group = o?.Operation?.Instrument?.Basis?.Name ?? o?.Operation?.Instrument?.Name,
          Time = o.Operation.Time,
          Side = o.Side,
          Size = o.Operation.Amount ?? 0,
          OpenPrice = o.Operation.AveragePrice ?? 0,
          ClosePrice = o?.Operation?.Instrument?.Price?.Last ?? 0,
          Gain = o.Gain ?? 0

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
