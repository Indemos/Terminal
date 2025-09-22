using Core.Common.Enums;
using Core.Common.Extensions;
using Core.Common.Services;
using Core.Common.States;
using Orleans;
using System;
using System.Collections.Generic;
using System.IO;
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
    Task<IList<OrderState>> Orders(MetaState criteria);

    /// <summary>
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="order"></param>
    Task<OrderResponse> Store(OrderState order);

    /// <summary>
    /// Remove order from the list
    /// </summary>
    /// <param name="order"></param>
    Task<DescriptorResponse> Remove(OrderState order);

    /// <summary>
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="price"></param>
    Task<StatusResponse> Tap(PriceState price);
  }

  public class OrdersGrain : Grain<OrdersState>, IOrdersGrain
  {
    /// <summary>
    /// Descriptor
    /// </summary>
    protected DescriptorState descriptor;

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
      descriptor = InstanceService<ConversionService>
        .Instance
        .Decompose<DescriptorState>(this.GetPrimaryKeyString());

      positions = GrainFactory.Get<IPositionsGrain>(descriptor);

      await base.OnActivateAsync(cancellation);
    }

    /// <summary>
    /// Get orders
    /// </summary>
    /// <param name="criteria"></param>
    public virtual async Task<IList<OrderState>> Orders(MetaState criteria) => await Task.WhenAll(State
      .Grains
      .Values
      .Select(o => o.Order()));

    /// <summary>
    /// <summary>
    /// Remove order from the list
    /// </summary>
    /// <param name="order"></param>
    public virtual Task<DescriptorResponse> Remove(OrderState order)
    {
      if (State.Grains.ContainsKey(order.Id))
      {
        State.Grains.Remove(order.Id);
      }

      return Task.FromResult(new DescriptorResponse
      {
        Data = order.Id
      });
    }

    /// <summary>
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="order"></param>
    public virtual async Task<OrderResponse> Store(OrderState order)
    {
      File.AppendAllText("D:/Code/NET/Terminal/demo", $"Grain - Send: {order.Id} {order.Operation.Instrument.Name} {DateTime.Now.Ticks} {Environment.NewLine}");

      var grain = GrainFactory.Get<IOrderGrain>(descriptor with { Order = order.Id });
      var response = await grain.Store(order);

      State.Grains[order.Id] = grain;

      return response;
    }

    /// <summary>
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="price"></param>
    public virtual async Task<StatusResponse> Tap(PriceState price)
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
  }
}
