using Core.Conventions;
using Core.Extensions;
using Core.Models;
using Orleans;
using Orleans.Streams;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Grains
{
  public interface IInstrumentGrain : IGrainWithStringKey
  {
    /// <summary>
    /// Get instrument
    /// </summary>
    Task<Instrument> Instrument();

    /// <summary>
    /// Store instrument
    /// </summary>
    /// <param name="instrument"></param>
    Task<Instrument> Send(Instrument instrument);

    /// <summary>
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    Task<PricesResponse> Prices(PriceCriteria criteria);

    /// <summary>
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    Task<PricesResponse> PriceGroups(PriceCriteria criteria);
  }

  public class InstrumentGrain : Grain<Prices>, IInstrumentGrain
  {
    /// <summary>
    /// Observer
    /// </summary>
    protected ITradeObserver observer;

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
    /// Get instrument
    /// </summary>
    public virtual Task<Instrument> Instrument() => Task.FromResult(State.Instrument);

    /// <summary>
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    public virtual Task<PricesResponse> Prices(PriceCriteria criteria) => Task.FromResult(new PricesResponse
    {
      Data = [.. State.Items]
    });

    /// <summary>
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    public virtual Task<PricesResponse> PriceGroups(PriceCriteria criteria) => Task.FromResult(new PricesResponse
    {
      Data = [.. State.ItemGroups]
    });

    /// <summary>
    /// Add price to the list
    /// </summary>
    /// <param name="instrument"></param>
    public virtual Task<Instrument> Send(Instrument instrument)
    {
      var nextPrice = instrument.Price;
      var currentPrice = State.ItemGroups.LastOrDefault() ?? new Price();
      var (price, expansion) = Combine(currentPrice, nextPrice, instrument.TimeFrame);

      if (expansion || State.ItemGroups.Count is 0)
      {
        State.ItemGroups.Add(price);
      }

      State.Items.Add(price);
      State.ItemGroups[^1] = price;
      State = State with { Instrument = instrument with { Price = price } };

      return Task.FromResult(State.Instrument);
    }

    /// <summary>
    /// Aggregate points
    /// </summary>
    /// <param name="currentPrice"></param>
    /// <param name="nextPrice"></param>
    /// <param name="span"></param>
    protected virtual (Price, bool) Combine(Price currentPrice, Price nextPrice, TimeSpan? span)
    {
      var nextTime = nextPrice.Time;
      var currentTime = currentPrice?.Bar?.Time ?? DateTime.MinValue.Ticks;
      var expansion = span is null || nextTime - currentTime >= span.Value.Ticks;
      var sidePrice = nextPrice.Bid ?? nextPrice?.Ask;
      var price = (nextPrice.Last ?? currentPrice.Last ?? sidePrice).Value;

      if (expansion)
      {
        currentPrice = nextPrice;
      }

      var group = new Price
      {
        Last = price,
        Time = nextPrice.Time,
        Volume = nextPrice.Volume,
        Ask = nextPrice.Ask ?? currentPrice?.Ask ?? price,
        Bid = nextPrice.Bid ?? currentPrice?.Bid ?? price,
        AskSize = nextPrice.AskSize ?? currentPrice?.AskSize ?? 0.0,
        BidSize = nextPrice.BidSize ?? currentPrice?.BidSize ?? 0.0,
        Bar = new() 
        {
          Close = price,
          Low = Math.Min(price, currentPrice?.Bar?.Low ?? price),
          High = Math.Max(price, currentPrice?.Bar?.High ?? price),
          Open = currentPrice?.Bar?.Open ?? price,
          Time = nextPrice.Time.Round(span)
        }
      };

      return (group, expansion);
    }
  }
}
