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
    /// List of points by criteria
    /// </summary>
    Task<PricesResponse> Prices();

    /// <summary>
    /// List of points by criteria
    /// </summary>
    Task<PricesResponse> PriceGroups();

    /// <summary>
    /// Add price to the list
    /// </summary>
    /// <param name="price"></param>
    Task<DescriptorResponse> Add(PriceState price);
  }

  public class PricesGrain : Grain<PricesState>, IPricesGrain
  {
    /// <summary>
    /// Instrument name
    /// </summary>
    protected string instrumentName;

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
      var descriptor = InstanceService<ConversionService>
        .Instance
        .Decompose<InstrumentDescriptor>(this.GetPrimaryKeyString());

      var dataStream = this
        .GetStreamProvider(nameof(StreamEnum.Price))
        .GetStream<PriceState>(descriptor.Account, Guid.Empty);

      instrumentName = descriptor.Instrument;
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
    /// List of points by criteria
    /// </summary>
    public Task<PricesResponse> Prices() => Task.FromResult(new PricesResponse
    {
      Data = State.Prices
    });

    /// <summary>
    /// List of points by criteria
    /// </summary>
    public Task<PricesResponse> PriceGroups() => Task.FromResult(new PricesResponse
    {
      Data = State.PriceGroups
    });

    /// <summary>
    /// Add price to the list
    /// </summary>
    /// <param name="price"></param>
    public Task<DescriptorResponse> Add(PriceState price)
    {
      var response = new DescriptorResponse { Data = price.Name };

      State.Prices.Add(price);

      if (price.Bar is not null)
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
    protected async Task OnPrice(PriceState price, StreamSequenceToken token)
    {
      if (Equals(price.Name, instrumentName))
      {
        await Add(price);
      }
    }
  }
}
