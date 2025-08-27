using Core.Client.Models;
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
  public partial class ControlsComponent
  {
    [Parameter] public virtual RenderFragment ChildContent { get; set; }

    public virtual IDictionary<string, IGateway> Adapters { get; set; } = new Dictionary<string, IGateway>();
    public virtual MessageModel<SubscriptionEnum> SubscriptionState { get; set; }

    /// <summary>
    /// Setup views
    /// </summary>
    /// <param name="setup"></param>
    protected override async Task OnAfterRenderAsync(bool setup)
    {
      await base.OnAfterRenderAsync(setup);

      if (setup)
      {
        SubscriptionState = new MessageModel<SubscriptionEnum>
        {
          Next = SubscriptionEnum.None,
        };

        StateHasChanged();
      }
    }

    public virtual async Task Connect()
    {
      try
      {
        SubscriptionState = new MessageModel<SubscriptionEnum>
        {
          Previous = SubscriptionEnum.None,
          Next = SubscriptionEnum.Progress,
        };

        await Task.WhenAll(Adapters.Values.Select(o => o.Connect()));

        SubscriptionState = new MessageModel<SubscriptionEnum>
        {
          Previous = SubscriptionEnum.Progress,
          Next = SubscriptionEnum.Stream,
        };
      }
      catch (Exception e)
      {
        InstanceService<MessageService>.Instance.Update(new MessageModel<string>
        {
          Error = e,
          Content = e.Message,
        });
      }
    }

    public virtual async Task Disconnect()
    {
      try
      {
        SubscriptionState = new MessageModel<SubscriptionEnum>
        {
          Previous = SubscriptionEnum.Stream,
          Next = SubscriptionEnum.Progress,
        };

        await Task.WhenAll(Adapters.Values.Select(o => o.Disconnect()));

        SubscriptionState = new MessageModel<SubscriptionEnum>
        {
          Previous = SubscriptionEnum.Progress,
          Next = SubscriptionEnum.None,
        };
      }
      catch (Exception e)
      {
        InstanceService<MessageService>.Instance.Update(new MessageModel<string>
        {
          Error = e,
          Content = e.Message,
        });
      }
    }

    public virtual async Task Subscribe()
    {
      try
      {
        SubscriptionState = new MessageModel<SubscriptionEnum>
        {
          Previous = SubscriptionEnum.Pause,
          Next = SubscriptionEnum.Progress,
        };

        await Task.WhenAll(Adapters.Values.Select(adapter => adapter.Subscribe()));

        SubscriptionState = new MessageModel<SubscriptionEnum>
        {
          Previous = SubscriptionEnum.Progress,
          Next = SubscriptionEnum.Stream,
        };
      }
      catch (Exception e)
      {
        InstanceService<MessageService>.Instance.Update(new MessageModel<string>
        {
          Error = e,
          Content = e.Message,
        });
      }
    }

    public virtual async Task Unsubscribe()
    {
      try
      {
        SubscriptionState = new MessageModel<SubscriptionEnum>
        {
          Previous = SubscriptionEnum.Stream,
          Next = SubscriptionEnum.Progress,
        };

        await Task.WhenAll(Adapters.Values.Select(adapter => adapter.Unsubscribe()));

        SubscriptionState = new MessageModel<SubscriptionEnum>
        {
          Previous = SubscriptionEnum.Progress,
          Next = SubscriptionEnum.Pause,
        };
      }
      catch (Exception e)
      {
        InstanceService<MessageService>.Instance.Update(new MessageModel<string>
        {
          Error = e,
          Content = e.Message,
        });
      }
    }
  }
}
