using Core.Common.Enums;
using Core.Common.Extensions;
using Core.Common.Services;
using Core.Common.States;
using Orleans;
using Orleans.Streams;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Common.Grains
{
  public interface IPriceGrain : IGrainWithStringKey
  {
    /// <summary>
    /// Get price data
    /// </summary>
    Task<PriceResponse> Price();

    /// <summary>
    /// Aggregate points
    /// </summary>
    /// <param name="price"></param>
    Task Store(PriceState price);
  }

  public class PriceGrain : Grain<PriceState>, IPriceGrain
  {
    /// <summary>
    /// Order stream
    /// </summary>
    protected IAsyncStream<PriceState> dataStream;

    /// <summary>
    /// Activation
    /// </summary>
    /// <param name="cancellation"></param>
    public override async Task OnActivateAsync(CancellationToken cancellation)
    {
      var descriptor = InstanceService<ConversionService>
        .Instance
        .Decompose<Descriptor>(this.GetPrimaryKeyString());

      dataStream = this
        .GetStreamProvider(nameof(StreamEnum.Price))
        .GetStream<PriceState>(descriptor.Account, Guid.Empty);

      State = new PriceState();

      await base.OnActivateAsync(cancellation);
    }

    /// <summary>
    /// Get price data
    /// </summary>
    public virtual Task<PriceResponse> Price() => Task.FromResult(new PriceResponse
    {
      Data = State
    });

    /// <summary>
    /// Aggregate points
    /// </summary>
    /// <param name="nextPoint"></param>
    public virtual async Task Store(PriceState nextPoint)
    {
      var currentPoint = State;
      var price = (nextPoint.Last ?? currentPoint.Last).Value;
      var nextTime = nextPoint.Time.Round(nextPoint.TimeFrame);
      var currentTime = currentPoint.Time.Round(nextPoint.TimeFrame);

      State = currentPoint with
      {
        Name = nextPoint.Name,
        Ask = nextPoint?.Ask ?? price,
        Bid = nextPoint?.Bid ?? price,
        AskSize = nextPoint?.AskSize ?? 0.0,
        BidSize = nextPoint?.BidSize ?? 0.0,
        Time = nextPoint?.Time,
        Last = price
      };

      if (nextTime > currentTime)
      {
        State = State with
        {
          Bar = new BarState() with
          {
            Close = price,
            Low = Math.Min(nextPoint.Bar?.Low ?? price, currentPoint?.Bar?.Low ?? price),
            High = Math.Max(nextPoint.Bar?.High ?? price, currentPoint?.Bar?.High ?? price),
            Open = nextTime > currentTime ? price : currentPoint?.Bar?.Open ?? price,
            Time = nextTime
          }
        };
      }

      await dataStream.OnNextAsync(State);
    }
  }
}
