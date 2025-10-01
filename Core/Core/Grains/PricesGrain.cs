using Core.Extensions;
using Core.Services;
using Core.Models;
using Orleans;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Grains
{
  public interface IPricesGrain : IGrainWithStringKey
  {
    /// <summary>
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<PriceModel>> Prices(MetaModel criteria);

    /// <summary>
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<PriceModel>> PriceGroups(MetaModel criteria);

    /// <summary>
    /// Add price to the list
    /// </summary>
    /// <param name="price"></param>
    Task<PriceModel> Store(PriceModel price);
  }

  public class PricesGrain : Grain<PricesModel>, IPricesGrain
  {
    /// <summary>
    /// Descriptor
    /// </summary>
    protected DescriptorModel descriptor;

    /// <summary>
    /// Converter
    /// </summary>
    protected ConversionService converter = new();

    /// <summary>
    /// Price map
    /// </summary>
    protected Dictionary<long?, int?> map = new();

    /// <summary>
    /// Activation
    /// </summary>
    /// <param name="cancellation"></param>
    public override async Task OnActivateAsync(CancellationToken cancellation)
    {
      descriptor = converter.Decompose<DescriptorModel>(this.GetPrimaryKeyString());

      await base.OnActivateAsync(cancellation);
    }

    /// <summary>
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    public virtual Task<IList<PriceModel>> Prices(MetaModel criteria) => Task.FromResult(State.Prices);

    /// <summary>
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    public virtual Task<IList<PriceModel>> PriceGroups(MetaModel criteria) => Task.FromResult(State.PriceGroups);

    /// <summary>
    /// Add price to the list
    /// </summary>
    /// <param name="nextPrice"></param>
    public virtual Task<PriceModel> Store(PriceModel nextPrice)
    {
      var roundTime = nextPrice.Time.Round(nextPrice.TimeFrame);
      var index = map.Get(roundTime);
      var currentPrice = index is null ? new PriceModel() : State.PriceGroups[index.Value];
      var currentTime = currentPrice.Time ?? DateTime.MinValue.Ticks;
      var price = Combine(currentPrice, nextPrice);

      State.Prices.Add(price);

      if (roundTime > currentTime)
      {
        State.PriceGroups.Add(price);
        map[roundTime.Value] = State.PriceGroups.Count - 1;
      }
      else if (index is not null)
      {
        State.PriceGroups[index.Value] = price;
      }

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
      var nextTime = nextPrice.Time.Round(nextPrice.TimeFrame);
      var currentTime = currentPrice.Time.Round(nextPrice.TimeFrame);

      return currentPrice with
      {
        Last = price,
        Time = nextPrice.Time,
        Name = nextPrice.Name,
        Ask = nextPrice.Ask ?? currentPrice?.Ask ?? price,
        Bid = nextPrice.Bid ?? currentPrice?.Bid ?? price,
        AskSize = nextPrice.AskSize ?? currentPrice?.AskSize ?? 0.0,
        BidSize = nextPrice.BidSize ?? currentPrice?.BidSize ?? 0.0,
        Bar = new BarModel() with
        {
          Close = price,
          Low = Math.Min(price, currentPrice?.Bar?.Low ?? price),
          High = Math.Max(price, currentPrice?.Bar?.High ?? price),
          Open = nextTime > currentTime ? price : currentPrice?.Bar?.Open ?? price,
          Time = nextTime
        }
      };
    }
  }
}
