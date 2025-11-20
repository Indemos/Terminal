using Core.Enums;
using Core.Models;
using Core.Services;
using Orleans;
using Orleans.Streams;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Grains
{
  public interface IConnectionGrain : IGrainWithStringKey
  {
    /// <summary>
    /// Connect
    /// </summary>
    Task<StatusResponse> Connect();

    /// <summary>
    /// Disconnect
    /// </summary>
    Task<StatusResponse> Disconnect();

    /// <summary>
    /// Subscribe to streams
    /// </summary>
    /// <param name="instrument"></param>
    Task<StatusResponse> Subscribe(Instrument instrument);

    /// <summary>
    /// Unsubscribe from streams
    /// </summary>
    /// <param name="instrument"></param>
    Task<StatusResponse> Unsubscribe(Instrument instrument);
  }

  /// <summary>
  /// Constructor
  /// </summary>
  public class ConnectionGrain : Grain, IConnectionGrain
  {
    /// <summary>
    /// HTTP service
    /// </summary>
    protected ConversionService converter = new();

    /// <summary>
    /// Messenger
    /// </summary>
    protected IAsyncStream<Message> messenger;

    /// <summary>
    /// Disposable connections
    /// </summary>
    protected List<IDisposable> connections = new();

    /// <summary>
    /// Activation
    /// </summary>
    /// <param name="cancellation"></param>
    public override async Task OnActivateAsync(CancellationToken cancellation)
    {
      messenger = this
        .GetStreamProvider(nameof(Message))
        .GetStream<Message>(string.Empty, Guid.Empty);

      await base.OnActivateAsync(cancellation);
    }

    /// <summary>
    /// Connect
    /// </summary>
    public virtual Task<StatusResponse> Connect() => Task.FromResult(new StatusResponse()
    {
      Data = StatusEnum.Active
    });

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public virtual Task<StatusResponse> Disconnect()
    {
      connections?.ForEach(o => o.Dispose());
      connections?.Clear();

      return Task.FromResult(new StatusResponse
      {
        Data = StatusEnum.Inactive
      });
    }

    /// <summary>
    /// Subscribe to streams
    /// </summary>
    /// <param name="instrument"></param>
    public virtual Task<StatusResponse> Subscribe(Instrument instrument) => Task.FromResult(new StatusResponse()
    {
      Data = StatusEnum.Active
    });

    /// <summary>
    /// Unsubscribe from streams
    /// </summary>
    /// <param name="instrument"></param>
    public virtual Task<StatusResponse> Unsubscribe(Instrument instrument) => Task.FromResult(new StatusResponse
    {
      Data = StatusEnum.Pause
    });
  }
}
