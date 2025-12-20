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
    /// <param name="criteria"></param>
    Task<Instrument> Instrument(Criteria criteria);

    /// <summary>
    /// Store instrument
    /// </summary>
    /// <param name="instrument"></param>
    Task<Instrument> Send(Instrument instrument);

    /// <summary>
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    Task<PricesResponse> Prices(Criteria criteria);

    /// <summary>
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    Task<PricesResponse> PriceGroups(Criteria criteria);
  }

  public class InstrumentGrain : Grain<Prices>, IInstrumentGrain
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
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    public virtual Task<PricesResponse> Prices(Criteria criteria)
    {
      var items = State.Items
        .Where(o => criteria?.MinDate is null || o.Time >= criteria.MinDate?.Ticks)
        .Where(o => criteria?.MaxDate is null || o.Time <= criteria.MaxDate?.Ticks)
        .TakeLast(criteria?.Count ?? State.Items.Count)
        .ToArray();

      return Task.FromResult(new PricesResponse
      {
        Data = items
      });
    }

    /// <summary>
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    public virtual Task<PricesResponse> PriceGroups(Criteria criteria)
    {
      var items = State.ItemGroups
        .Where(o => criteria?.MinDate is null || o.Time >= criteria.MinDate?.Ticks)
        .Where(o => criteria?.MaxDate is null || o.Time <= criteria.MaxDate?.Ticks)
        .TakeLast(criteria?.Count ?? State.ItemGroups.Count)
        .ToArray();

      return Task.FromResult(new PricesResponse
      {
        Data = items
      });
    }

    /// <summary>
    /// Get instrument
    /// </summary>
    /// <param name="criteria"></param>
    public virtual Task<Instrument> Instrument(Criteria criteria) => Task.FromResult(State.Instrument);

    /// <summary>
    /// Add price to the list
    /// </summary>
    /// <param name="instrument"></param>
    public virtual Task<Instrument> Send(Instrument instrument)
    {
      var span = instrument.TimeFrame;
      var nextPrice = instrument.Price;
      var nextTime = nextPrice.Time.Round(span);
      var currentPrice = State.ItemGroups.LastOrDefault() ?? new Price();
      var currentTime = currentPrice.Time.Round(span) ?? DateTime.MinValue.Ticks;
      var price = Combine(nextTime > currentTime ? nextPrice : currentPrice, nextPrice, span);

      if (span is null || nextTime - currentTime >= span.Value.Ticks)
      {
        State.ItemGroups.Add(price);
      }

      State.Items.Add(price);
      State.ItemGroups[State.ItemGroups.Count - 1] = price;
      State = State with { Instrument = instrument with { Price = price } };

      return Task.FromResult(State.Instrument);
    }

    /// <summary>
    /// Aggregate points
    /// </summary>
    /// <param name="currentPrice"></param>
    /// <param name="nextPrice"></param>
    /// <param name="nextTime"></param>
    protected virtual Price Combine(Price currentPrice, Price nextPrice, TimeSpan? nextTime)
    {
      var price = (nextPrice.Last ?? currentPrice.Last).Value;

      return currentPrice with
      {
        Last = price,
        Time = nextPrice.Time,
        Ask = nextPrice.Ask ?? currentPrice?.Ask ?? price,
        Bid = nextPrice.Bid ?? currentPrice?.Bid ?? price,
        AskSize = nextPrice.AskSize ?? currentPrice?.AskSize ?? 0.0,
        BidSize = nextPrice.BidSize ?? currentPrice?.BidSize ?? 0.0,
        Bar = new Bar() with
        {
          Close = price,
          Low = Math.Min(price, currentPrice?.Bar?.Low ?? price),
          High = Math.Max(price, currentPrice?.Bar?.High ?? price),
          Open = currentPrice?.Bar?.Open ?? price,
          Time = nextPrice.Time.Round(nextTime)
        }
      };
    }
  }
}
