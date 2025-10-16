using Core.Enums;
using Core.Extensions;
using Core.Models;
using Core.Services;
using Core.Validators;
using Orleans;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Grains
{
  public interface IOrdersGrain : IGrainWithStringKey
  {
    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    Task<IList<OrderModel>> Orders(MetaModel criteria);

    /// <summary>
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="order"></param>
    Task<OrderGroupsResponse> Store(OrderModel order);

    /// <summary>
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="price"></param>
    Task<StatusResponse> Tap(PriceModel price);

    /// <summary>
    /// Remove order from the list
    /// </summary>
    /// <param name="order"></param>
    Task<DescriptorResponse> Clear(OrderModel order);

    /// <summary>
    /// Clear orders
    /// </summary>
    Task<StatusResponse> Clear();
  }

  public class OrdersGrain : Grain<OrdersModel>, IOrdersGrain
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
    /// Order validator
    /// </summary>
    protected OrderValidator orderValidator = new();

    /// <summary>
    /// Transactions
    /// </summary>
    protected IPositionsGrain positions;

    /// <summary>
    /// Instruments
    /// </summary>
    protected Dictionary<string, PriceModel> prices = new();

    /// <summary>
    /// Activation
    /// </summary>
    /// <param name="cleaner"></param>
    public override async Task OnActivateAsync(CancellationToken cleaner)
    {
      descriptor = converter.Decompose<DescriptorModel>(this.GetPrimaryKeyString());
      positions = GrainFactory.Get<IPositionsGrain>(descriptor);

      await base.OnActivateAsync(cleaner);
    }

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    public virtual async Task<IList<OrderModel>> Orders(MetaModel criteria) => await Task.WhenAll(State
      .Grains
      .Values
      .Select(o => o.Order()));

    /// <summary>
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="order"></param>
    public virtual async Task<OrderGroupsResponse> Store(OrderModel order)
    {
      var orders = Compose(order);
      var responses = orders
        .Select(o => new OrderResponse { Data = o, Errors = [.. Errors(o).Select(error => error.Message)] })
        .ToArray();

      if (responses.Sum(o => o.Errors.Count) is 0)
      {
        foreach (var o in orders)
        {
          var name = descriptor with { Order = o.Id };
          var grain = GrainFactory.Get<IOrderGrain>(name);

          await grain.Store(o);

          State.Grains[order.Id] = grain;
        }
      }

      return new()
      {
        Data = responses
      };
    }

    /// <summary>
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="price"></param>
    public virtual async Task<StatusResponse> Tap(PriceModel price)
    {
      prices[price.Name] = price;

      foreach (var grain in State.Grains)
      {
        if (await grain.Value.IsExecutable(price))
        {
          State.Grains.Remove(grain.Key);
          await positions.Store(await grain.Value.Position(price));
        }
      }

      return new()
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Remove order from the list
    /// </summary>
    /// <param name="order"></param>
    public virtual Task<DescriptorResponse> Clear(OrderModel order)
    {
      State.Grains.Remove(order.Id);

      return Task.FromResult(new DescriptorResponse
      {
        Data = order.Id
      });
    }

    /// <summary>
    /// Clear orders
    /// </summary>
    public virtual Task<StatusResponse> Clear()
    {
      State.Grains.Clear();

      return Task.FromResult(new StatusResponse
      {
        Data = StatusEnum.Inactive
      });
    }

    /// <summary>
    /// Convert hierarchy of orders into a plain list
    /// </summary>
    protected virtual List<OrderModel> Compose(OrderModel order)
    {
      var nextOrders = order
        .Orders
        .Where(o => o.Instruction is null)
        .Select(o => Merge(o, order))
        .ToList();

      if (order.Amount is not null || order.Orders.Count is 0)
      {
        nextOrders.Add(Merge(order, order));
      }

      return nextOrders;
    }

    /// <summary>
    /// Update side order from group
    /// </summary>
    /// <param name="group"></param>
    /// <param name="order"></param>
    protected virtual OrderModel Merge(OrderModel group, OrderModel order)
    {
      var instrument =
        order?.Operation?.Instrument ??
        group?.Operation?.Instrument;

      var groupOrders = order
        ?.Orders
        ?.Where(o => o.Instruction is InstructionEnum.Brace)
        ?.Select(o => Merge(group, o));

      var nextOrder = order with
      {
        Descriptor = group.Descriptor,
        Orders = [.. groupOrders],
        Operation = order.Operation with
        {
          Time = instrument?.Price?.Time,
          Instrument = instrument with { Price = prices.Get(instrument.Name) },
        }
      };

      return nextOrder;
    }

    /// <summary>
    /// Preprocess order
    /// </summary>
    /// <param name="order"></param>
    protected virtual List<ErrorModel> Errors(OrderModel order)
    {
      var response = new List<ErrorModel>();
      var orders = order.Orders.Append(order);

      foreach (var subOrder in orders)
      {
        var errors = orderValidator
          .Validate(subOrder)
          .Errors
          .Select(error => new ErrorModel { Message = error.ErrorMessage });

        response.AddRange(errors);
      }

      return response;
    }
  }
}
