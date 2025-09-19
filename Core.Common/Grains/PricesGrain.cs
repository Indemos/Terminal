using Core.Common.Extensions;
using Core.Common.Services;
using Core.Common.States;
using Orleans;
using System;
using System.Collections.Generic;
using System.Linq;
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
    protected Dictionary<long, int> map = new();

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
      var currentPrice = new PriceState();
      var roundTime = nextPrice.Time.Round(nextPrice.TimeFrame);

      if (map.TryGetValue(roundTime.Value, out var index))
      {
        currentPrice = State.PriceGroups[index];
      }

      var currentTime = currentPrice.Time ?? DateTime.MinValue.Ticks;
      var price = Combine(currentPrice, nextPrice);

      State.Prices.Add(price);

      if (roundTime > currentTime)
      {
        State.PriceGroups.Add(price);
        map[roundTime.Value] = State.PriceGroups.Count - 1;
      }
      else
      {
        State.PriceGroups[index] = price;
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
        Name = nextPrice.Name,
        Ask = nextPrice?.Ask ?? price,
        Bid = nextPrice?.Bid ?? price,
        AskSize = nextPrice?.AskSize ?? 0.0,
        BidSize = nextPrice?.BidSize ?? 0.0,
        Time = nextPrice?.Time,
        Last = price,
        Bar = new BarState() with
        {
          Close = price,
          Low = Math.Min(nextPrice.Bar?.Low ?? price, currentPrice?.Bar?.Low ?? price),
          High = Math.Max(nextPrice.Bar?.High ?? price, currentPrice?.Bar?.High ?? price),
          Open = nextTime > currentTime ? price : currentPrice?.Bar?.Open ?? price,
          Time = nextTime
        }
      };
    }
  }
}
