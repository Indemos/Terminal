using Core.Common.Enums;
using Core.Common.Services;
using Core.Common.States;
using Orleans;
using Orleans.Streams;
using System;
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
    Task<PricesResponse> Prices(MetaState criteria);

    /// <summary>
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    Task<PricesResponse> PriceGroups(MetaState criteria);

    /// <summary>
    /// Add price to the list
    /// </summary>
    /// <param name="price"></param>
    Task<DescriptorResponse> Add(PriceState price);
  }

  public class PricesGrain : Grain<PricesState>, IPricesGrain
  {
    /// <summary>
    /// Descriptor
    /// </summary>
    protected InstrumentDescriptor descriptor;

    /// <summary>
    /// Data subscription
    /// </summary>
    protected StreamSubscriptionHandle<PriceState> dataSubscription;

    /// <summary>
    /// Activation
    /// </summary>
    /// <param name="cancellation"></param>
    public override async Task OnActivateAsync(CancellationToken cancellation)
    {
      descriptor = InstanceService<ConversionService>
        .Instance
        .Decompose<InstrumentDescriptor>(this.GetPrimaryKeyString());

      var dataStream = this
        .GetStreamProvider(nameof(StreamEnum.Price))
        .GetStream<PriceState>(descriptor.Account, Guid.Empty);

      dataSubscription = await dataStream.SubscribeAsync(OnPrice);

      await base.OnActivateAsync(cancellation);
    }

    /// <summary>
    /// Deactivation
    /// </summary>
    /// <param name="cancellation"></param>
    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellation)
    {
      if (dataSubscription is not null)
      {
        await dataSubscription.UnsubscribeAsync();
      }

      await base.OnDeactivateAsync(reason, cancellation);
    }

    /// <summary>
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    public virtual Task<PricesResponse> Prices(MetaState criteria) => Task.FromResult(new PricesResponse
    {
      Data = State.Prices
    });

    /// <summary>
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    public virtual Task<PricesResponse> PriceGroups(MetaState criteria)
    {
      return Task.FromResult(new PricesResponse
      {
        Data = State.PriceGroups
      });
    }

    /// <summary>
    /// Add price to the list
    /// </summary>
    /// <param name="price"></param>
    public virtual Task<DescriptorResponse> Add(PriceState price)
    {
      var response = new DescriptorResponse { Data = price.Name };

      State.Prices.Add(price);

      if (price.Bar is not null || price.TimeFrame is not null)
      {
        State.PriceGroups.Add(price);
      }

      return Task.FromResult(response);
    }

    /// <summary>
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="price"></param>
    /// <param name="token"></param>
    protected virtual async Task OnPrice(PriceState price, StreamSequenceToken token)
    {
      if (Equals(price.Name, descriptor.Instrument))
      {
        await Add(price);
      }
    }
  }
}
