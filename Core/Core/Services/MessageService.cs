using Core.Extensions;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Services
{
  public class MessageService
  {
    /// <summary>
    /// Subscriptions
    /// </summary>
    protected ConcurrentDictionary<Type, ConcurrentDictionary<string, Func<object, Task>>> subscriptions = new();

    /// <summary>
    /// Subscribe to messages
    /// </summary>
    public virtual void Subscribe<T>(Func<T, Task> action, string name = "")
    {
      subscriptions[typeof(T)] = subscriptions.Get(typeof(T)) ?? new();
      subscriptions[typeof(T)][name] = o => action((T)o);
    }

    /// <summary>
    /// Unsubscribe from messages
    /// </summary>
    public virtual void Unsubscribe<T>(string name)
    {
      if (subscriptions.TryGetValue(typeof(T), out var actions))
      {
        actions.TryRemove(name, out _);
      }
    }

    /// <summary>
    /// Send message without state change
    /// </summary>
    public virtual Task Send<T>(T state)
    {
      if (subscriptions.TryGetValue(typeof(T), out var actions))
      {
        return Task.WhenAll(actions.Values.Select(o => o(state)));
      }

      return Task.CompletedTask;
    }
  }
}
