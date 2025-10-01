using Core.Client.Services;
using Core.Common.Conventions;
using Core.Common.Enums;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Client.Components
{
  public partial class PositionsComponent
  {
    [Inject] public virtual MessageService Messenger { get; set; }

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
        Messenger.OnMessage += state =>
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
    public virtual async void UpdateItems(params IGateway[] adapters)
    {
      if (Messenger.State.Next is SubscriptionEnum.None)
      {
        return;
      }

      if (Update.IsCompleted)
      {
        var queries = adapters.Select(o => o.Positions(default));
        var responses = await Task.WhenAll(queries);
        var positions = responses
          .SelectMany(o => o)
          .OrderBy(o => o.Operation.Time)
          .ToList();

        Items = [.. positions.Select(o => new Row
        {
          Name = o?.Operation?.Instrument?.Name,
          Group = o?.Operation?.Instrument?.Basis?.Name ?? o?.Operation?.Instrument?.Name,
          Time = new DateTime(o.Operation.Time.Value),
          Side = o.Side,
          Size = o.Operation.Amount ?? 0,
          OpenPrice = o.Operation.AveragePrice ?? 0,
          ClosePrice = o?.Operation?.Instrument?.Price?.Last ?? 0,
          Gain = o.Balance.Current ?? 0

        })];

        Update = Task.WhenAll([InvokeAsync(StateHasChanged)]);
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
