using Core.Conventions;
using Core.Enums;
using Core.Extensions;
using Core.Models;
using Core.Services;
using Orleans;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Grains
{
  public interface ITransactionsGrain : IGrainWithStringKey
  {
    /// <summary>
    /// Setup
    /// </summary>
    /// <param name="observer"></param>
    Task<StatusResponse> Setup(ITradeObserver observer);

    /// <summary>
    /// Get transactions
    /// </summary>
    /// <param name="criteria"></param>
    Task<OrdersResponse> Transactions(Criteria criteria);

    /// <summary>
    /// Add to the list
    /// </summary>
    /// <param name="order"></param>
    Task<OrderResponse> Store(Order order);
  }

  /// <summary>
  /// Constructor
  /// </summary>
  /// <param name="messenger"></param>
  public class TransactionsGrain : Grain<Transactions>, ITransactionsGrain
  {
    /// <summary>
    /// Observer
    /// </summary>
    protected ITradeObserver observer;

    /// <summary>
    /// Setup
    /// </summary>
    /// <param name="grainObserver"></param>
    public virtual Task<StatusResponse> Setup(ITradeObserver grainObserver)
    {
      observer = grainObserver;

      return Task.FromResult(new StatusResponse
      {
        Data = StatusEnum.Active
      });
    }

    /// <summary>
    /// Get transactions
    /// </summary>
    /// <param name="criteria"></param>
    public virtual async Task<OrdersResponse> Transactions(Criteria criteria)
    {
      var items = await Task.WhenAll(State
        .Grains
        .Select(o => o.Transaction()));

      return new OrdersResponse
      {
        Data = items
      };
    }

    /// <summary>
    /// Add to the list
    /// </summary>
    /// <param name="order"></param>
    public virtual async Task<OrderResponse> Store(Order order)
    {
      var descriptor = this.GetDescriptor(order.Operation.Id);
      var grain = GrainFactory.GetGrain<ITransactionGrain>(descriptor);
      var response = await grain.Store(order);

      State.Grains.Add(grain);

      await observer.StreamOrder(order);

      return response;
    }
  }
}
