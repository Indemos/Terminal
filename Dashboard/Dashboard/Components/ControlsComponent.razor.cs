using Core.Conventions;
using Core.Enums;
using Core.Models;
using Core.Services;
using Microsoft.AspNetCore.Components;
using Orleans;
using Orleans.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dashboard.Components
{
  public partial class ControlsComponent
  {
    [Inject] IClusterClient Connector { get; set; }
    [Inject] StateService Subscription { get; set; }
    [Inject] NavigationManager Navigator { get; set; }

    [Parameter] public virtual RenderFragment ChildContent { get; set; }
    [Parameter] public virtual IDictionary<string, IGateway> Adapters { get; set; } = new Dictionary<string, IGateway>();

    /// <summary>
    /// Messenger
    /// </summary>
    IAsyncStream<Message> Messenger { get; set; }

    /// <summary>
    /// Setup views
    /// </summary>
    /// <param name="setup"></param>
    protected override async Task OnAfterRenderAsync(bool setup)
    {
      await base.OnAfterRenderAsync(setup);

      if (setup)
      {
        Messenger = Connector
          .GetStreamProvider(nameof(Message))
          .GetStream<Message>(string.Empty, Guid.Empty);

        Subscription.State = new()
        {
          Next = SubscriptionEnum.None
        };

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
        await Subscription.Send(new()
        {
          Previous = SubscriptionEnum.None,
          Next = SubscriptionEnum.Progress
        });

        await Task.WhenAll(Adapters.Values.Select(o => o.Connect()));

        await Subscription.Send(new()
        {
          Previous = SubscriptionEnum.Progress,
          Next = SubscriptionEnum.Stream
        });
      }
      catch (Exception e)
      {
        await Messenger.OnNextAsync(new Message() { Error = e, Description = e.Message });
      }
    }

    /// <summary>
    /// Disconnect
    /// </summary>
    public virtual async Task Disconnect()
    {
      try
      {
        await Subscription.Send(new()
        {
          Previous = SubscriptionEnum.Stream,
          Next = SubscriptionEnum.Progress,
        });

        await Task.WhenAll(Adapters.Values.Select(o => o.Disconnect()));

        await Subscription.Send(new()
        {
          Previous = SubscriptionEnum.Progress,
          Next = SubscriptionEnum.None,
        });
      }
      catch (Exception e)
      {
        await Messenger.OnNextAsync(new Message() { Error = e, Description = e.Message });
      }
    }

    /// <summary>
    /// Subscribe
    /// </summary>
    public virtual async Task Subscribe()
    {
      try
      {
        await Subscription.Send(new()
        {
          Previous = SubscriptionEnum.Pause,
          Next = SubscriptionEnum.Progress,
        });

        await Task.WhenAll(Adapters.Values.Select(adapter => adapter.Subscribe()));

        await Subscription.Send(new()
        {
          Previous = SubscriptionEnum.Progress,
          Next = SubscriptionEnum.Stream,
        });
      }
      catch (Exception e)
      {
        await Messenger.OnNextAsync(new Message() { Error = e, Description = e.Message });
      }
    }

    /// <summary>
    /// Unsubscribe
    /// </summary>
    public virtual async Task Unsubscribe()
    {
      try
      {
        await Subscription.Send(new()
        {
          Previous = SubscriptionEnum.Stream,
          Next = SubscriptionEnum.Progress,
        });

        await Task.WhenAll(Adapters.Values.Select(adapter => adapter.Unsubscribe()));

        await Subscription.Send(new()
        {
          Previous = SubscriptionEnum.Progress,
          Next = SubscriptionEnum.Pause,
        });
      }
      catch (Exception e)
      {
        await Messenger.OnNextAsync(new Message() { Error = e, Description = e.Message });
      }
    }
  }
}
