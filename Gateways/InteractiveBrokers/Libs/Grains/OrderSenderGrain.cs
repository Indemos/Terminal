using Core.Extensions;
using Core.Grains;
using Core.Models;
using InteractiveBrokers.Mappers;
using System.Threading.Tasks;

namespace InteractiveBrokers.Grains
{
  public interface IInterOrderSenderGrain : IInterOrdersGrain
  {
    /// <summary>
    /// Send order
    /// </summary>
    /// <param name="order"></param>
    Task<OrderResponse> SendOrder(Core.Models.Order order);

    /// <summary>
    /// Clear order
    /// </summary>
    /// <param name="order"></param>
    Task<DescriptorResponse> ClearOrder(Core.Models.Order order);
  }

  public class InterOrderSenderGrain : InterOrdersGrain, IInterOrderSenderGrain
  {
    /// <summary>
    /// Send order
    /// </summary>
    /// <param name="order"></param>
    public virtual async Task<OrderResponse> SendOrder(Core.Models.Order order)
    {
      var contract = Upstream.MapContract(order.Operation.Instrument);
      var (orderMessage, SL, TP) = Upstream.MapOrder(order, state.Account);
      var (group, braces) = connector.SendOrder(contract, orderMessage, SL, TP);

      order = order with { Operation = order.Operation with { Id = $"{group.OrderId}" } };

      await Task.Delay(state.Span);

      return new()
      {
        Data = order
      };
    }

    /// <summary>
    /// Clear order
    /// </summary>
    /// <param name="order"></param>
    public virtual async Task<DescriptorResponse> ClearOrder(Core.Models.Order order)
    {
      var descriptor = this.GetDescriptor();
      var ordersGrain = GrainFactory.GetGrain<IOrdersGrain>(descriptor);

      connector.ClearOrder(int.Parse(order.Id));

      await ordersGrain.Clear(order);
      await Task.Delay(state.Span);

      return new()
      {
        Data = order.Id
      };
    }
  }
}
