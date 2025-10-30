using Core.Enums;
using Core.Models;
using System;
using System.Threading.Tasks;

namespace Core.Services
{
  public class StateService
  {
    /// <summary>
    /// State notification
    /// </summary>
    public virtual Func<SubscriptionModel, Task> Update { get; set; } = o => Task.CompletedTask;

    /// <summary>
    /// Subscription instance
    /// </summary>
    public virtual SubscriptionModel State { get; set; } = new() { Next = SubscriptionEnum.None };

    /// <summary>
    /// Trigger
    /// </summary>
    public virtual void Subscribe(Func<SubscriptionModel, Task> action) => Update += action;

    /// <summary>
    /// Trigger
    /// </summary>
    public virtual Task Send(SubscriptionModel state) => Update(State = state);
  }
}
