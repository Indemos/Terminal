using Core.Enums;
using Core.Extensions;
using Core.Services;
using Core.Models;
using Orleans;
using Orleans.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Grains
{
  public interface ITransactionsGrain : IGrainWithStringKey
  {
    /// <summary>
    /// Get transactions
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<OrderModel>> Transactions(MetaModel criteria);

    /// <summary>
    /// Add to the list
    /// </summary>
    /// <param name="order"></param>
    Task<OrderResponse> Store(OrderModel order);
  }

  public class TransactionsGrain : Grain<TransactionsModel>, ITransactionsGrain
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
    /// Order stream
    /// </summary>
    protected IAsyncStream<OrderModel> orderStream;

    /// <summary>
    /// Activation
    /// </summary>
    /// <param name="cancellation"></param>
    public override async Task OnActivateAsync(CancellationToken cancellation)
    {
      descriptor = converter.Decompose<DescriptorModel>(this.GetPrimaryKeyString());
      orderStream = this
        .GetStreamProvider(nameof(StreamEnum.Order))
        .GetStream<OrderModel>(descriptor.Account, Guid.Empty);

      await base.OnActivateAsync(cancellation);
    }

    /// <summary>
    /// Get transactions
    /// </summary>
    /// <param name="criteria"></param>
    public virtual async Task<IList<OrderModel>> Transactions(MetaModel criteria) => await Task.WhenAll(State
      .Grains
      .Select(o => o.Transaction()));

    /// <summary>
    /// Add to the list
    /// </summary>
    /// <param name="order"></param>
    public virtual async Task<OrderResponse> Store(OrderModel order)
    {
      var orderGrain = GrainFactory.Get<ITransactionGrain>(descriptor with { Order = order.Id });
      var response = await orderGrain.Store(order);

      State.Grains.Add(orderGrain);

      await orderStream.OnNextAsync(order);

      return response;
    }
  }
}
