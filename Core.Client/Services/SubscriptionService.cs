using Core.Client.Models;
using Core.Common.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Client.Services
{
  public class SubscriptionService
  {
    /// <summary>
    /// Async observers
    /// </summary>
    protected readonly List<Func<MessageModel<SubscriptionEnum>, Task>> observers = [];

    /// <summary>
    /// Async observers
    /// </summary>
    protected readonly List<Func<MessageModel<SubscriptionEnum>, Task>> asyncObservers = [];

    /// <summary>
    /// Subscription instance
    /// </summary>
    public virtual MessageModel<SubscriptionEnum> State { get; protected set; }

    /// <summary>
    /// Push notification
    /// </summary>
    public virtual Action<MessageModel<SubscriptionEnum>> OnMessage { get; set; } = o => { };

    /// <summary>
    /// Push notification
    /// </summary>
    public virtual event Func<MessageModel<SubscriptionEnum>, Task> OnMessageAsync
    {
      add => asyncObservers.Add(value);
      remove => asyncObservers.Remove(value);
    }

    /// <summary>
    /// Send notification
    /// </summary>
    public virtual Task Send(MessageModel<SubscriptionEnum> value)
    {
      State = value;
      OnMessage(State);

      return Task.WhenAll(asyncObservers.ToArray().Select(o => o(State)));
    }
  }
}
