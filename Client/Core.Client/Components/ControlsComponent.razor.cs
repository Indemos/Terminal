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
  public partial class ControlsComponent
  {
    [Inject] public virtual MessageService Messenger { get; set; }
    [Parameter] public virtual RenderFragment ChildContent { get; set; }

    public virtual IDictionary<string, IGateway> Adapters { get; set; } = new Dictionary<string, IGateway>();

    /// <summary>
    /// Setup views
    /// </summary>
    /// <param name="setup"></param>
    protected override async Task OnAfterRenderAsync(bool setup)
    {
      await base.OnAfterRenderAsync(setup);

      if (setup)
      {
        Messenger.State = new() { Next = SubscriptionEnum.None };

        StateHasChanged();
      }
    }

    /// <summary>
    /// Connect
    /// </summary>
    public virtual async Task Connect()
    {
      try
      {
        Messenger.State = new()
        {
          Previous = SubscriptionEnum.None,
          Next = SubscriptionEnum.Progress
        };

        await Task.WhenAll(Adapters.Values.Select(o => o.Connect()));

        Messenger.State = new()
        {
          Previous = SubscriptionEnum.Progress,
          Next = SubscriptionEnum.Stream
        };
      }
      catch (Exception e)
      {
        await Messenger.Stream.OnNextAsync(new() { Error = e, Content = e.Message });
      }
    }

    /// <summary>
    /// Disconnect
    /// </summary>
    public virtual async Task Disconnect()
    {
      try
      {
        Messenger.State = new()
        {
          Previous = SubscriptionEnum.Stream,
          Next = SubscriptionEnum.Progress,
        };

        await Task.WhenAll(Adapters.Values.Select(o => o.Disconnect()));

        Messenger.State = new()
        {
          Previous = SubscriptionEnum.Progress,
          Next = SubscriptionEnum.None,
        };
      }
      catch (Exception e)
      {
        await Messenger.Stream.OnNextAsync(new() { Error = e, Content = e.Message });
      }
    }

    /// <summary>
    /// Subscribe
    /// </summary>
    public virtual async Task Subscribe()
    {
      try
      {
        Messenger.State = new()
        {
          Previous = SubscriptionEnum.Pause,
          Next = SubscriptionEnum.Progress,
        };

        await Task.WhenAll(Adapters.Values.Select(adapter => adapter.Subscribe()));

        Messenger.State = new()
        {
          Previous = SubscriptionEnum.Progress,
          Next = SubscriptionEnum.Stream,
        };
      }
      catch (Exception e)
      {
        await Messenger.Stream.OnNextAsync(new() { Error = e, Content = e.Message });
      }
    }

    /// <summary>
    /// Unsubscribe
    /// </summary>
    public virtual async Task Unsubscribe()
    {
      try
      {
        Messenger.State = new()
        {
          Previous = SubscriptionEnum.Stream,
          Next = SubscriptionEnum.Progress,
        };

        await Task.WhenAll(Adapters.Values.Select(adapter => adapter.Unsubscribe()));

        Messenger.State = new()
        {
          Previous = SubscriptionEnum.Progress,
          Next = SubscriptionEnum.Pause,
        };
      }
      catch (Exception e)
      {
        await Messenger.Stream.OnNextAsync(new() { Error = e, Content = e.Message });
      }
    }
  }
}
