using Core.Models;
using Orleans;
using Orleans.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Grains
{
  public interface IOptionsGrain : IGrainWithStringKey
  {
    /// <summary>
    /// Option chain
    /// </summary>
    /// <param name="criteria"></param>
    Task<InstrumentsResponse> Options(Criteria criteria);

    /// <summary>
    /// Update options
    /// </summary>
    /// <param name="options"></param>
    Task<StatusResponse> Store(List<Instrument> options);
  }

  public class OptionsGrain : Grain<Instruments>, IOptionsGrain
  {
    /// <summary>
    /// Messenger
    /// </summary>
    protected IAsyncStream<Message> messenger;

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
    /// Option chain
    /// </summary>
    /// <param name="criteria"></param>
    public virtual Task<InstrumentsResponse> Options(Criteria criteria)
    {
      var side = criteria?.Instrument?.Derivative?.Side;
      var options = State
        .Items
        .Where(o => side is null || Equals(o.Derivative.Side, side))
        .Where(o => criteria?.MinDate is null || o.Derivative.ExpirationDate?.Date >= criteria?.MinDate?.Date)
        .Where(o => criteria?.MaxDate is null || o.Derivative.ExpirationDate?.Date <= criteria?.MaxDate?.Date)
        .Where(o => criteria?.MinPrice is null || o.Derivative.Strike >= criteria.MinPrice)
        .Where(o => criteria?.MaxPrice is null || o.Derivative.Strike <= criteria.MaxPrice)
        .OrderBy(o => o.Derivative.ExpirationDate)
        .ThenBy(o => o.Derivative.Strike)
        .ThenBy(o => o.Derivative.Side)
        .ToArray();

      return Task.FromResult(new InstrumentsResponse
      {
        Data = options
      });
    }

    /// <summary>
    /// Update options
    /// </summary>
    /// <param name="options"></param>
    public virtual Task<StatusResponse> Store(List<Instrument> options)
    {
      if (options is not null)
      {
        State = State with
        {
          Items = options
        };
      }

      return Task.FromResult(new StatusResponse
      {
        Data = Enums.StatusEnum.Active
      });
    }
  }
}
