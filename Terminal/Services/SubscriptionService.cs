using System;
using Terminal.Core.Enums;
using Terminal.Core.Models;

namespace Terminal.Services
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
