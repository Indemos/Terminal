using Core.Enums;
using Core.Models;
using Orleans;
using Orleans.Streams;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Grains
{
  public interface IDomGrain : IGrainWithStringKey
  {
    /// <summary>
    /// Get DOM
    /// </summary>
    /// <param name="criteria"></param>
    Task<DomResponse> Dom(DomCriteria criteria);

    /// <summary>
    /// Update DOM
    /// </summary>
    /// <param name="dom"></param>
    Task<StatusResponse> Store(Dom dom);
  }

  public class DomGrain : Grain<Dom>, IDomGrain
  {
    /// <summary>
    /// Messenger
    /// </summary>
    protected IAsyncStream<Message> messenger;

    /// <summary>
    /// Activation
    /// </summary>
    /// <param name="cts"></param>
    public override async Task OnActivateAsync(CancellationToken cts)
    {
      messenger = this
        .GetStreamProvider(nameof(Message))
        .GetStream<Message>(string.Empty, Guid.Empty);

      await base.OnActivateAsync(cts);
    }

    /// <summary>
    /// Get DOM
    /// </summary>
    /// <param name="criteria"></param>
    public virtual Task<DomResponse> Dom(DomCriteria criteria)
    {
      var response = new DomResponse
      {
        Data = State
      };

      return Task.FromResult(response);
    }

    /// <summary>
    /// Update DOM
    /// </summary>
    /// <param name="dom"></param>
    public virtual Task<StatusResponse> Store(Dom dom)
    {
      if (dom is not null)
      {
        State = State with
        {
          Bids = dom.Bids,
          Asks = dom.Asks,
        };
      }

      return Task.FromResult(new StatusResponse
      {
        Data = StatusEnum.Active
      });
    }
  }
}
