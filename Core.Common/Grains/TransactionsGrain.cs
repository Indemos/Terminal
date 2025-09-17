using Core.Common.Enums;
using Core.Common.Extensions;
using Core.Common.Services;
using Core.Common.States;
using Orleans;
using Orleans.Streams;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Common.Grains
{
  public interface ITransactionsGrain : IGrainWithStringKey
  {
    /// <summary>
    /// Get transactions
    /// </summary>
    /// <param name="criteria"></param>
    Task<OrdersResponse> Transactions(MetaState criteria);

    /// <summary>
    /// Add to the list
    /// </summary>
    /// <param name="order"></param>
    Task<DescriptorResponse> Store(OrderState order);
  }

  public class TransactionsGrain : Grain<TransactionsState>, ITransactionsGrain
  {
    /// <summary>
    /// Descriptor
    /// </summary>
    protected DescriptorState descriptor;

    /// <summary>
    /// Order stream
    /// </summary>
    protected IAsyncStream<OrderState> orderStream;

    /// <summary>
    /// Activation
    /// </summary>
    /// <param name="cancellation"></param>
    public override async Task OnActivateAsync(CancellationToken cancellation)
    {
      descriptor = InstanceService<ConversionService>
        .Instance
        .Decompose<DescriptorState>(this.GetPrimaryKeyString());

      orderStream = this
        .GetStreamProvider(nameof(StreamEnum.Order))
        .GetStream<OrderState>(descriptor.Account, Guid.Empty);

      await base.OnActivateAsync(cancellation);
    }

    /// <summary>
    /// Get transactions
    /// </summary>
    /// <param name="criteria"></param>
    public virtual async Task<OrdersResponse> Transactions(MetaState criteria) => new OrdersResponse
    {
      Data = await Task.WhenAll(State.Grains.Select(o => o.Transaction()))
    };

    /// <summary>
    /// Add to the list
    /// </summary>
    /// <param name="order"></param>
    public virtual async Task<DescriptorResponse> Store(OrderState order)
    {
      var orderGrain = GrainFactory.Get<ITransactionGrain>(descriptor with { Order = order.Id });

      State.Grains.Add(orderGrain);

      await orderGrain.Store(order);
      await orderStream.OnNextAsync(order);

      return new DescriptorResponse
      {
        Data = order.Id
      };
    }
  }
}
