using Core.Extensions;
using Core.Models;
using Orleans;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Grains
{
  public interface IInstrumentGrain : IGrainWithStringKey
  {
    /// <summary>
    /// Get instrument
    /// </summary>
    /// <param name="criteria"></param>
    Task<InstrumentModel> Instrument(CriteriaModel criteria);

    /// <summary>
    /// Store instrument
    /// </summary>
    /// <param name="instrument"></param>
    Task<InstrumentModel> Store(InstrumentModel instrument);

    /// <summary>
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<PriceModel>> Prices(CriteriaModel criteria);

    /// <summary>
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<PriceModel>> PriceGroups(CriteriaModel criteria);
  }

  public class InstrumentGrain : Grain<PricesModel>, IInstrumentGrain
  {
    /// <summary>
    /// Instrument
    /// </summary>
    protected InstrumentModel instrument;

    /// <summary>
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    public virtual Task<IList<PriceModel>> Prices(CriteriaModel criteria) => Task.FromResult(State.Prices);

    /// <summary>
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    public virtual Task<IList<PriceModel>> PriceGroups(CriteriaModel criteria) => Task.FromResult(State.PriceGroups);

    /// <summary>
    /// Get instrument
    /// </summary>
    /// <param name="criteria"></param>
    public virtual Task<InstrumentModel> Instrument(CriteriaModel criteria) => Task.FromResult(instrument);

    /// <summary>
    /// Add price to the list
    /// </summary>
    /// <param name="instrument"></param>
    public virtual Task<InstrumentModel> Store(InstrumentModel instrument)
    {
      var span = instrument.TimeFrame;
      var nextPrice = instrument.Price;
      var nextTime = nextPrice.Time.Round(span);
      var currentPrice = State.PriceGroups.LastOrDefault() ?? new PriceModel();
      var currentTime = currentPrice.Time.Round(span) ?? DateTime.MinValue.Ticks;
      var price = Combine(nextTime > currentTime ? nextPrice : currentPrice, nextPrice, span);

      if (span is null || nextTime - currentTime >= span.Value.Ticks)
      {
        State.PriceGroups.Add(price);
      }

      State.Prices.Add(price);
      State.PriceGroups[State.PriceGroups.Count - 1] = price;
      State = State with { Instrument = instrument with { Price = price } };

      return Task.FromResult(State.Instrument);
    }

    /// <summary>
    /// Aggregate points
    /// </summary>
    /// <param name="currentPrice"></param>
    /// <param name="nextPrice"></param>
    /// <param name="nextTime"></param>
    protected virtual PriceModel Combine(PriceModel currentPrice, PriceModel nextPrice, TimeSpan? nextTime)
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
        Bar = new BarModel() with
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
