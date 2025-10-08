using MessagePack;
using MessagePack.Resolvers;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Core.Conventions
{
  public class StreamMessenger<T> : IAsyncDisposable
  {
    protected string name;

    protected HubConnection connection;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="source"></param>
    /// <param name="descriptor"></param>
    public StreamMessenger(string source, string descriptor)
    {
      name = descriptor;
      connection = new HubConnectionBuilder()
        .WithUrl(source)
        .WithAutomaticReconnect()
        .ConfigureLogging(o =>
        {
          o.AddConsole();
          o.SetMinimumLevel(LogLevel.Error);
        })
        .AddMessagePackProtocol(o => o.SerializerOptions = MessagePackSerializerOptions
          .Standard
          .WithResolver(ContractlessStandardResolver.Instance))
        .Build();
    }

    /// <summary>
    /// Send message
    /// </summary>
    /// <param name="message"></param>
    public virtual Task Send(T message) => Connect().ContinueWith(o => connection.SendAsync(nameof(StreamHub.Send), name, message));

    /// <summary>
    /// Subscribe to messages
    /// </summary>
    /// <param name="action"></param>
    public virtual Task Subscribe(Action<T> action) => Connect().ContinueWith(o => connection.On(name, action));

    /// <summary>
    /// Dispose
    /// </summary>
    public virtual ValueTask DisposeAsync() => connection.DisposeAsync();

    /// <summary>
    /// Connect
    /// </summary>
    public virtual async Task Connect()
    {
      if (connection.State is not HubConnectionState.Connected)
      {
        await connection.StartAsync().ConfigureAwait(false);
      }
    }
  }
}
