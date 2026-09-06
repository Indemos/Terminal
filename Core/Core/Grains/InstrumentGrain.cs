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
    Task<InstrumentResponse> Send(Instrument instrument);

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
    /// List of price groups by criteria
    /// </summary>
    /// <param name="criteria"></param>
    public virtual Task<PricesResponse> PriceGroups(PriceCriteria criteria) => Prices(criteria);

    /// <summary>
    /// Add price to the list
    /// </summary>
    /// <param name="instrument"></param>
    public virtual Task<InstrumentResponse> Send(Instrument instrument)
    {
      var nextPrice = instrument.Price;
      var currentPrice = State.Items.LastOrDefault() ?? new Price();
      var price = Combine(currentPrice, nextPrice);

      State.Items.Add(price);
      State = State with { Instrument = instrument with { Price = price } };

      return Task.FromResult(new InstrumentResponse
      {
        Data = State.Instrument
      });
    }

    /// <summary>
    /// Aggregate points
    /// </summary>
    /// <param name="currentPrice"></param>
    /// <param name="nextPrice"></param>
    protected virtual Price Combine(Price currentPrice, Price nextPrice)
    {
      var sidePrice = nextPrice.Bid ?? nextPrice?.Ask;
      var price = (nextPrice.Last ?? currentPrice.Last ?? sidePrice).Value;
      var group = new Price
      {
        Last = price,
        Time = nextPrice.Time,
        Volume = nextPrice.Volume,
        Ask = nextPrice.Ask ?? currentPrice?.Ask ?? price,
        Bid = nextPrice.Bid ?? currentPrice?.Bid ?? price,
        AskSize = nextPrice.AskSize ?? currentPrice?.AskSize ?? 0.0,
        BidSize = nextPrice.BidSize ?? currentPrice?.BidSize ?? 0.0
      };

      return group;
    }
  }
}
