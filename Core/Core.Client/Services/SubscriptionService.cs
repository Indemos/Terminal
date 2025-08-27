using Core.Client.Models;
using Core.Common.Enums;
using System;

namespace Core.Client.Services
{
  public class SubscriptionService
  {
    private MessageModel<SubscriptionEnum> state;

    /// <summary>
    /// Push notification
    /// </summary>
    public virtual Action<MessageModel<SubscriptionEnum>> Update { get; set; } = delegate { };

    /// <summary>
    /// Subscription instance
    /// </summary>
    public virtual MessageModel<SubscriptionEnum> State
    {
      get => state;
      set => Update(state = value);
    }
  }
}
