using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Models;
using Terminal.Core.Services;
using Terminal.Services;

namespace Terminal.Components
{
  public partial class ControlsComponent
  {
    [Inject] SubscriptionService Observer { get; set; }

    [Parameter] public virtual RenderFragment ChildContent { get; set; }

    public virtual IDictionary<string, IGateway> Adapters { get; set; } = new Dictionary<string, IGateway>();
    public virtual MessageModel<SubscriptionEnum> SubscriptionState
    {
      get => Observer.State ?? new MessageModel<SubscriptionEnum> { Next = SubscriptionEnum.None };
      set => Observer.State = value;
    }

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
