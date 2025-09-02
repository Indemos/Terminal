using Core.Client.Models;
using Core.Client.Services;
using Core.Common.Enums;
using Core.Common.Implementations;
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
    [Inject] public virtual SubscriptionService Observer { get; set; }
    [Parameter] public virtual RenderFragment ChildContent { get; set; }

    public virtual IDictionary<string, IGateway> Adapters { get; set; } = new Dictionary<string, IGateway>();
    public virtual MessageModel<SubscriptionEnum> SubscriptionState => Observer.State ?? new MessageModel<SubscriptionEnum>
    {
      Next = SubscriptionEnum.None
    };

    /// <summary>
    /// Setup views
    /// </summary>
    /// <param name="setup"></param>
    protected override async Task OnAfterRenderAsync(bool setup)
    {
      await base.OnAfterRenderAsync(setup);

      if (setup)
      {
        await Observer.Send(new MessageModel<SubscriptionEnum>
        {
          Next = SubscriptionEnum.None
        });

        StateHasChanged();
      }
    }

    public virtual async Task Connect()
    {
      try
      {
        await Observer.Send(new MessageModel<SubscriptionEnum>
        {
          Previous = SubscriptionEnum.None,
          Next = SubscriptionEnum.Progress,
        });

        await Task.WhenAll(Adapters.Values.Select(o => o.Connect()));
        await Observer.Send(new MessageModel<SubscriptionEnum>
        {
          Previous = SubscriptionEnum.Progress,
          Next = SubscriptionEnum.Stream,
        });
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
        await Observer.Send(new MessageModel<SubscriptionEnum>
        {
          Previous = SubscriptionEnum.Stream,
          Next = SubscriptionEnum.Progress,
        });

        await Task.WhenAll(Adapters.Values.Select(o => o.Disconnect()));
        await Observer.Send(new MessageModel<SubscriptionEnum>
        {
          Previous = SubscriptionEnum.Progress,
          Next = SubscriptionEnum.None,
        });
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
        await Observer.Send(new MessageModel<SubscriptionEnum>
        {
          Previous = SubscriptionEnum.Pause,
          Next = SubscriptionEnum.Progress,
        });

        await Task.WhenAll(Adapters.Values.Select(adapter => adapter.Subscribe()));
        await Observer.Send(new MessageModel<SubscriptionEnum>
        {
          Previous = SubscriptionEnum.Progress,
          Next = SubscriptionEnum.Stream,
        });
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
        await Observer.Send(new MessageModel<SubscriptionEnum>
        {
          Previous = SubscriptionEnum.Stream,
          Next = SubscriptionEnum.Progress,
        });

        await Task.WhenAll(Adapters.Values.Select(adapter => adapter.Unsubscribe()));
        await Observer.Send(new MessageModel<SubscriptionEnum>
        {
          Previous = SubscriptionEnum.Progress,
          Next = SubscriptionEnum.Pause,
        });
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
