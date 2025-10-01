using Core.Common.Enums;
using Core.Common.Extensions;
using Core.Common.Services;
using Core.Common.Models;
using Orleans;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Common.Grains
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
    Task<OrderResponse> Store(OrderModel order);

    /// <summary>
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="price"></param>
    Task<StatusResponse> Tap(PriceModel price);

    /// <summary>
    /// Remove order from the list
    /// </summary>
    /// <param name="order"></param>
    Task<DescriptorResponse> Remove(OrderModel order);
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
    /// Transactions
    /// </summary>
    protected IPositionsGrain positions;

    /// <summary>
    /// Activation
    /// </summary>
    /// <param name="cancellation"></param>
    public override async Task OnActivateAsync(CancellationToken cancellation)
    {
      descriptor = converter.Decompose<DescriptorModel>(this.GetPrimaryKeyString());
      positions = GrainFactory.Get<IPositionsGrain>(descriptor);

      await base.OnActivateAsync(cancellation);
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
    public virtual async Task<OrderResponse> Store(OrderModel order)
    {
      var grain = State.Grains[order.Id] = GrainFactory.Get<IOrderGrain>(descriptor with
      {
        Order = order.Id
      });

      return await grain.Store(order);
    }

    /// <summary>
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="price"></param>
    public virtual async Task<StatusResponse> Tap(PriceModel price)
    {
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
    /// <summary>
    /// Remove order from the list
    /// </summary>
    /// <param name="order"></param>
    public virtual Task<DescriptorResponse> Remove(OrderModel order)
    {
      State.Grains.Remove(order.Id);

      return Task.FromResult(new DescriptorResponse
      {
        Data = order.Id
      });
    }
  }
}
