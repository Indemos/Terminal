using Core.Common.Extensions;
using Core.Common.Services;
using Core.Common.States;
using Orleans;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Common.Grains
{
  public interface IPricesGrain : IGrainWithStringKey
  {
    /// <summary>
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<PriceState>> Prices(MetaState criteria);

    /// <summary>
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<PriceState>> PriceGroups(MetaState criteria);

    /// <summary>
    /// Add price to the list
    /// </summary>
    /// <param name="price"></param>
    Task<PriceState> Store(PriceState price);
  }

  public class PricesGrain : Grain<PricesState>, IPricesGrain
  {
    /// <summary>
    /// Descriptor
    /// </summary>
    protected DescriptorState descriptor;

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
      descriptor = InstanceService<ConversionService>
        .Instance
        .Decompose<DescriptorState>(this.GetPrimaryKeyString());

      await base.OnActivateAsync(cancellation);
    }

    /// <summary>
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    public virtual Task<IList<PriceState>> Prices(MetaState criteria) => Task.FromResult(State.Prices);

    /// <summary>
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    public virtual Task<IList<PriceState>> PriceGroups(MetaState criteria) => Task.FromResult(State.PriceGroups);

    /// <summary>
    /// Add price to the list
    /// </summary>
    /// <param name="nextPrice"></param>
    public virtual Task<PriceState> Store(PriceState nextPrice)
    {
      var roundTime = nextPrice.Time.Round(nextPrice.TimeFrame);
      var index = map.Get(roundTime);
      var currentPrice = index is null ? new PriceState() : State.PriceGroups[index.Value];
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
    protected virtual PriceState Combine(PriceState currentPrice, PriceState nextPrice)
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
        Bar = new BarState() with
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
