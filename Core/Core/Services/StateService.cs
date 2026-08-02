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
    public virtual Func<Subscription, Task> Update { get; set; } = o => Task.CompletedTask;

    /// <summary>
    /// Subscription instance
    /// </summary>
    public virtual Subscription State { get; set; } = new() { Next = SubscriptionEnum.None };

    /// <summary>
    /// Trigger
    /// </summary>
    public virtual void Subscribe(Func<Subscription, Task> action) => Update += action;

    /// <summary>
    /// Trigger
    /// </summary>
    public virtual Task Send(Subscription state) => Update(State = state);
  }
}
