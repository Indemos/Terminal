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
    Task<OrderState[]> Transactions();

    /// <summary>
    /// Add to the list
    /// </summary>
    /// <param name="order"></param>
    Task Send(OrderState order);
  }

  public class TransactionsGrain : Grain<TransactionsState>, ITransactionsGrain
  {
    /// <summary>
    /// Descriptor
    /// </summary>
    protected Descriptor descriptor;

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
        .Decompose<Descriptor>(this.GetPrimaryKeyString());

      orderStream = this
        .GetStreamProvider(nameof(StreamEnum.Order))
        .GetStream<OrderState>(descriptor.Account, Guid.Empty);

      await base.OnActivateAsync(cancellation);
    }

    /// <summary>
    /// Get transactions
    /// </summary>
    public async Task<OrderState[]> Transactions()
    {
      return await Task.WhenAll(State.Grains.Select(o => o.Transaction()));
    }

    /// <summary>
    /// Add to the list
    /// </summary>
    /// <param name="order"></param>
    public async Task Send(OrderState order)
    {
      var orderDescriptor = new OrderDescriptor
      {
        Account = descriptor.Account,
        Order = order.Id
      };

      var orderGrain = GrainFactory.Get<ITransactionGrain>(orderDescriptor);

      await orderGrain.Store(order);
      await orderStream.OnNextAsync(order);

      State.Grains.Add(orderGrain);
    }
  }
}
