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
    /// <typeparam name="T"></typeparam>
    /// <param name="action"></param>
    /// <param name="descriptor"></param>
    public virtual string Subscribe<T>(Func<T, Task> action, string descriptor)
    {
      var group = typeof(T);
      
      subscriptions[group] = subscriptions.Get(group) ?? new();
      subscriptions[group][descriptor] = o => action((T)o);

      return descriptor;
    }

    /// <summary>
    /// Unsubscribe
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="name"></param>
    public virtual void Unsubscribe<T>(string name)
    {
      if (subscriptions.TryGetValue(typeof(T), out var actions))
      {
        actions.TryRemove(name, out _);
      }
    }

    /// <summary>
    /// Send message
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="state"></param>
    public virtual async Task Send<T>(T state)
    {
      if (subscriptions.TryGetValue(typeof(T), out var actions))
      {
        await Task.WhenAll(actions.Values.Select(o => o(state)));
      }
    }
  }
}
