using Core.Models;
using Core.Services;
using Orleans;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Grains
{
  public interface ITransactionsGrain : IGrainWithStringKey
  {
    /// <summary>
    /// Get transactions
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<OrderModel>> Transactions(CriteriaModel criteria);

    /// <summary>
    /// Add to the list
    /// </summary>
    /// <param name="order"></param>
    Task<OrderResponse> Store(OrderModel order);
  }

  /// <summary>
  /// Constructor
  /// </summary>
  /// <param name="messenger"></param>
  public class TransactionsGrain(MessageService messenger) : Grain<TransactionsModel>, ITransactionsGrain
  {
    /// <summary>
    /// Messenger
    /// </summary>
    protected MessageService messenger = messenger;

    /// <summary>
    /// Get transactions
    /// </summary>
    /// <param name="criteria"></param>
    public virtual async Task<IList<OrderModel>> Transactions(CriteriaModel criteria) => await Task.WhenAll(State
      .Grains
      .Select(o => o.Transaction()));

    /// <summary>
    /// Add to the list
    /// </summary>
    /// <param name="order"></param>
    public virtual async Task<OrderResponse> Store(OrderModel order)
    {
      var descriptor = $"{this.GetPrimaryKeyString()}:{order.Operation.Id}";
      var grain = GrainFactory.GetGrain<ITransactionGrain>(descriptor);
      var response = await grain.Store(order);

      State.Grains.Add(grain);

      await messenger.Send(order);

      return response;
    }
  }
}
