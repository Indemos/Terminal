using Core.Enums;
using Core.Models;
using Orleans;
using Orleans.Streams;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Grains
{
  public interface IDomGrain : IGrainWithStringKey
  {
    /// <summary>
    /// Get DOM
    /// </summary>
    /// <param name="criteria"></param>
    Task<DomResponse> Dom(DomCriteria criteria);

    /// <summary>
    /// Update DOM
    /// </summary>
    /// <param name="dom"></param>
    Task<StatusResponse> Store(Dom dom);

    /// <summary>
    /// Update DOM with order
    /// </summary>
    /// <param name="order"></param>
    Task<StatusResponse> SendOrder(DomOrder order);

    /// <summary>
    /// Remove order from the book
    /// </summary>
    /// <param name="order"></param>
    Task<DomOrderResponse> RemoveOrder(DomOrder order);

    /// <summary>
    /// Store order in the book
    /// </summary>
    /// <param name="order"></param>
    Task<DomOrderResponse> StoreOrder(DomOrder order);

    /// <summary>
    /// Clear state
    /// </summary>
    Task<StatusResponse> Clear();
  }

  public class DomGrain : Grain<Dom>, IDomGrain
  {
    /// <summary>
    /// Messenger
    /// </summary>
    protected IAsyncStream<Message> messenger;

    /// <summary>
    /// Order book with suborders
    /// </summary>
    protected Dictionary<string, LinkedListNode<DomOrder>> orderIndex = new();

    /// <summary>
    /// Activation
    /// </summary>
    /// <param name="cts"></param>
    public override async Task OnActivateAsync(CancellationToken cts)
    {
      messenger = this
        .GetStreamProvider(nameof(Message))
        .GetStream<Message>(string.Empty, Guid.Empty);

      await base.OnActivateAsync(cts);
    }

    /// <summary>
    /// Get DOM
    /// </summary>
    /// <param name="criteria"></param>
    public virtual Task<DomResponse> Dom(DomCriteria criteria)
    {
      var response = new DomResponse
      {
        Data = State
      };

      return Task.FromResult(response);
    }

    /// <summary>
    /// Update DOM
    /// </summary>
    /// <param name="dom"></param>
    public virtual Task<StatusResponse> Store(Dom dom)
    {
      if (dom is not null)
      {
        orderIndex.Clear();
        State = dom;
      }

      return Task.FromResult(new StatusResponse
      {
        Data = StatusEnum.Active
      });
    }

    /// <summary>
    /// Update DOM with order
    /// </summary>
    /// <param name="order"></param>
    public virtual async Task<StatusResponse> SendOrder(DomOrder order)
    {
      switch (order.Action)
      {
        case DomAction.Store:
        case DomAction.Update: await StoreOrder(order); break;
        case DomAction.Remove: await RemoveOrder(order); break;
        case DomAction.Clear: await Clear(); break;
      }

      return new()
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Store order in the book
    /// </summary>
    /// <param name="order"></param>
    public virtual async Task<DomOrderResponse> StoreOrder(DomOrder order)
    {
      var response = new DomOrderResponse();

      if (order?.Id is null)
      {
        return response;
      }

      orderIndex.TryGetValue(order.Id, out var node);

      var domOrder = node?.Value;
      var orderPrice = order.Price ?? domOrder?.Price;
      var orderSide = order.Side ?? domOrder?.Side;
      var orderSize = order.Size ?? domOrder?.Size;

      if (orderSize <= 0)
      {
        return await RemoveOrder(order);
      }

      var side = Side(orderSide);
      var price = Price(orderPrice);

      if (price is null || side is null)
      {
        return response;
      }

      var mergeOrder = (domOrder ?? order) with
      {
        Size = orderSize,
        Side = orderSide,
        Price = orderPrice,
        Action = order.Action,
        Name = order.Name ?? domOrder?.Name,
        Mask = order.Mask ?? domOrder?.Mask,
        Index = order.Index ?? domOrder?.Index,
        Source = order.Source ?? domOrder?.Source
      };

      if (domOrder is not null && domOrder.Side == orderSide && domOrder.Price == orderPrice && domOrder.Size >= orderSize)
      {
        node.Value = mergeOrder;
        return response;
      }

      var prices = side.GetValueOrDefault(price.Value);

      if (prices is null)
      {
        side[price.Value] = prices = new LinkedList<DomOrder>();
      }

      if (domOrder is not null)
      {
        await RemoveOrder(domOrder);
      }

      orderIndex[order.Id] = prices.AddLast(mergeOrder);

      return response;
    }

    /// <summary>
    /// Remove order from the book
    /// </summary>
    /// <param name="order"></param>
    /// <summary>
    /// Remove order from the book or partially reduce its size in place.
    /// </summary>
    /// <param name="order"></param>
    public virtual Task<DomOrderResponse> RemoveOrder(DomOrder order)
    {
      var response = new DomOrderResponse();

      if (order?.Id is null || orderIndex.TryGetValue(order.Id, out var node) is false)
      {
        return Task.FromResult(response);
      }

      var domOrder = node.Value;
      var size = domOrder.Size - (order.Size ?? domOrder.Size);

      // 1. Reduce

      if (order.Size is not null && size > 0)
      {
        node.Value = domOrder with { Size = size };
        return Task.FromResult(response);
      }

      // 2. Remove

      if (node.List is not null)
      {
        node.List.Remove(node);

        var side = Side(domOrder.Side);
        var price = Price(domOrder.Price);

        if (node.List.Count is 0 && price.HasValue && side is not null)
        {
          side.Remove(price.Value);
        }
      }

      orderIndex.Remove(domOrder.Id);

      return Task.FromResult(response);
    }

    /// <summary>
    /// Clear state
    /// </summary>
    public virtual Task<StatusResponse> Clear()
    {
      State.Bids.Clear();
      State.Asks.Clear();
      orderIndex.Clear();

      return Task.FromResult(new StatusResponse
      {
        Data = StatusEnum.Active
      });
    }

    /// <summary>
    /// Precise price without decimals
    /// </summary>
    /// <param name="side"></param>
    protected virtual SortedDictionary<long, LinkedList<DomOrder>> Side(DomSide? side)
    {
      switch (side)
      {
        case DomSide.Bid: return State.Bids;
        case DomSide.Ask: return State.Asks;
      }

      return null;
    }

    /// <summary>
    /// Precise price without decimals
    /// </summary>
    /// <param name="price"></param>
    protected virtual long? Price(double? price)
    {
      if (price is not null)
      {
        return (long)Math.Round(price.Value * 10000);
      }

      return null;
    }
  }
}
