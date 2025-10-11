using MessagePack;
using MessagePack.Resolvers;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Core.Services
{
  public class StreamService : IAsyncDisposable
  {
    protected HubConnection connection;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="source"></param>
    public StreamService(string source)
    {
      connection = new HubConnectionBuilder()
        .WithUrl(source)
        .WithAutomaticReconnect()
        .ConfigureLogging(o =>
        {
          o.AddConsole();
          o.SetMinimumLevel(LogLevel.Critical);
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
    public virtual Task Send<T>(T message) => Connect().ContinueWith(o => connection.SendAsync(nameof(StreamHubService.Send), typeof(T).Name, message));

    /// <summary>
    /// Subscribe to messages
    /// </summary>
    /// <param name="action"></param>
    public virtual Task Subscribe<T>(Action<T> action) => Connect().ContinueWith(o => connection.On(typeof(T).Name, action));

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
