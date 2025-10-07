using Core.Enums;
using Core.Models;
using System;

namespace Core.Services
{
  public class SubscriptionService
  {
    protected SubscriptionModel state = new() { Next = SubscriptionEnum.None };

    /// <summary>
    /// State notification
    /// </summary>
    public virtual Action<SubscriptionModel> OnState { get; set; } = delegate { };

    /// <summary>
    /// Subscription instance
    /// </summary>
    public virtual SubscriptionModel State
    {
      get => state;
      set => OnState(state = value);
    }
  }
}
