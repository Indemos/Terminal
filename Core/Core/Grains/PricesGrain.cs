using Core.Extensions;
using Core.Models;
using Orleans;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Grains
{
  public interface IPricesGrain : IGrainWithStringKey
  {
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

    /// <summary>
    /// Add price to the list
    /// </summary>
    /// <param name="price"></param>
    Task<PriceModel> Store(PriceModel price);
  }

  public class PricesGrain : Grain<PricesModel>, IPricesGrain
  {
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
    /// Add price to the list
    /// </summary>
    /// <param name="nextPrice"></param>
    public virtual Task<PriceModel> Store(PriceModel nextPrice)
    {
      var nextTime = nextPrice.Time.Round(nextPrice.TimeFrame);
      var currentPrice = State.PriceGroups.LastOrDefault() ?? new PriceModel();
      var currentTime = currentPrice.Time.Round(nextPrice.TimeFrame) ?? DateTime.MinValue.Ticks;
      var price = Combine(nextTime > currentTime ? nextPrice : currentPrice, nextPrice);

      State.Prices.Add(price);

      if (nextPrice.TimeFrame is null || nextTime - currentTime >= nextPrice.TimeFrame.Value.Ticks)
      {
        State.PriceGroups.Add(price);
      }

      State.PriceGroups[State.PriceGroups.Count - 1] = price;

      return Task.FromResult(price);
    }

    /// <summary>
    /// Aggregate points
    /// </summary>
    /// <param name="currentPrice"></param>
    /// <param name="nextPrice"></param>
    protected virtual PriceModel Combine(PriceModel currentPrice, PriceModel nextPrice)
    {
      var price = (nextPrice.Last ?? currentPrice.Last).Value;

      return currentPrice with
      {
        Last = price,
        Name = nextPrice.Name,
        Time = nextPrice.Time,
        TimeFrame = nextPrice.TimeFrame,
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
          Time = nextPrice.Time.Round(nextPrice.TimeFrame)
        }
      };
    }
  }
}
