using Core.Extensions;
using Core.Messengers;
using Core.Models;
using Core.Services;
using Orleans;
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
    protected Messenger streamer;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="streamService"></param>
    public TransactionsGrain(Messenger streamService) => streamer = streamService;

    /// <summary>
    /// Activation
    /// </summary>
    /// <param name="cleaner"></param>
    public override async Task OnActivateAsync(CancellationToken cleaner)
    {
      descriptor = converter.Decompose<DescriptorModel>(this.GetPrimaryKeyString());
      await base.OnActivateAsync(cleaner);
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

      await streamer.Orders.Send(order);

      return response;
    }
  }
}
