using Core.Common.Extensions;
using Core.Common.States;
using Orleans;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Common.Grains
{
  public class PricesGrain : Grain<PricesState>, IGrainWithStringKey
  {
    /// <summary>
    /// Get price data
    /// </summary>
    public Task<PriceResponse> GetPrice() => Task.FromResult(new PriceResponse
    {
      Data = State.Prices.LastOrDefault()
    });

    /// <summary>
    /// List of points by criteria, e.g. for specified instrument
    /// </summary>
    public Task<PricesResponse> GetPriceGroups() => Task.FromResult(new PricesResponse
    {
      Data = State.PriceGroups
    });

    /// <summary>
    /// List of points by criteria, e.g. for specified instrument
    /// </summary>
    public Task<PricesResponse> GetPrices() => Task.FromResult(new PricesResponse
    {
      Data = State.Prices
    });

    /// <summary>
    /// Aggregate points
    /// </summary>
    public Task Store(PriceState point)
    {
      var currentPrice = point.Last;
      var previousPrice = State.Instrument.Price.Last;
      var price = (currentPrice ?? previousPrice).Value;
      var min = Math.Min(point.Bar?.Low ?? price, State.Instrument.Price?.Bar?.Low ?? price);
      var max = Math.Max(point.Bar?.High ?? price, State.Instrument.Price?.Bar?.High ?? price);
      var currentTime = point.Time.Round(point.TimeFrame);
      var previousTime = point.Time.Round(point.TimeFrame);
      var openPrice = currentTime > previousTime ? price : State.Instrument.Price?.Bar?.Open ?? price;

      var bar = State.Instrument.Price.Bar with
      {
        Close = price,
        Low = Math.Min(min, price),
        High = Math.Max(max, price),
        Open = openPrice
      };

      var pointUpdate = State.Instrument.Price with
      {
        Bar = bar,
        Ask = point?.Ask ?? price,
        Bid = point?.Bid ?? price,
        AskSize = point?.AskSize ?? 0.0,
        BidSize = point?.BidSize ?? 0.0,
        Time = currentTime ?? point?.Time
      };

      State = State with
      {
        Instrument = State.Instrument with { Price = pointUpdate }
      };

      State.Prices.Add(point);

      if (currentTime > previousTime)
      {
        State.PriceGroups.Add(State.Instrument.Price);
      }

      return Task.CompletedTask;
    }
  }
}
