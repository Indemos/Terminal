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
  public interface IPositionGrain : IGrainWithStringKey
  {
    /// <summary>
    /// Estimated PnL in account's currency for one side of the order
    /// </summary>
    Task<double> Gain();

    /// <summary>
    /// Estimated PnL in points for one side of the order
    /// </summary>
    Task<double> Range();

    /// <summary>
    /// Get order state
    /// </summary>
    Task<OrderState> Position();

    /// <summary>
    /// Create position
    /// </summary>
    /// <param name="order"></param>
    Task StorePosition(OrderState order);

    /// <summary>
    /// Match positions
    /// </summary>
    /// <param name="order"></param>
    Task<DescriptorResponse> Combine(OrderState order);
  }

  public class PositionGrain : Grain<OrderState>, IPositionGrain
  {
    /// <summary>
    /// Order stream
    /// </summary>
    protected IAsyncStream<OrderState> orderStream;

    /// <summary>
    /// Data subscription
    /// </summary>
    protected StreamSubscriptionHandle<PriceState> dataSubscription;

    /// <summary>
    /// Activation
    /// </summary>
    /// <param name="cancellation"></param>
    public override async Task OnActivateAsync(CancellationToken cancellation)
    {
      var converter = InstanceService<ConversionService>.Instance;
      var baseDescriptor = converter.Decompose<BaseDescriptor>(this.GetPrimaryKeyString());
      var dataStream = this
        .GetStreamProvider(nameof(StreamEnum.Price))
        .GetStream<PriceState>(baseDescriptor.Account, Guid.Empty);

      orderStream = this
        .GetStreamProvider(nameof(StreamEnum.Order))
        .GetStream<OrderState>(baseDescriptor.Account, Guid.Empty);

      dataSubscription = await dataStream.SubscribeAsync(OnPrice);

      await base.OnActivateAsync(cancellation);
    }

    /// <summary>
    /// Deactivation
    /// </summary>
    /// <param name="cancellation"></param>
    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellation)
    {
      if (dataSubscription is not null)
      {
        await dataSubscription.UnsubscribeAsync();
      }

      await base.OnDeactivateAsync(reason, cancellation);
    }

    /// <summary>
    /// Get order state
    /// </summary>
    public Task<OrderState> Position()
    {
      return Task.FromResult(State);
    }

    /// <summary>
    /// Estimated PnL in account's currency for one side of the order
    /// </summary>
    public async Task<double> Gain()
    {
      var pointEstimate = await Range();
      var amount = State.Operation.Amount;
      var instrument = State.Operation.Instrument;
      var estimate = amount * pointEstimate * instrument.Leverage - instrument.Commission;
      var gain = estimate ?? State.Gain ?? 0;

      State = State with
      {
        Gain = gain,
        Min = Math.Min(State.Min ?? 0, gain),
        Max = Math.Max(State.Max ?? 0, gain)
      };

      return estimate.Value;
    }

    /// <summary>
    /// Estimated PnL in points for one side of the order
    /// </summary>
    public Task<double> Range()
    {
      var price = State.Operation.Price ?? ClosePrice();
      var estimate = (price - State.Operation.AveragePrice) * Side();

      return Task.FromResult(estimate.Value);
    }

    /// <summary>
    /// Create position
    /// </summary>
    /// <param name="order"></param>
    public async Task StorePosition(OrderState order)
    {
      var converter = InstanceService<ConversionService>.Instance;
      var baseDescriptor = converter.Decompose<BaseDescriptor>(this.GetPrimaryKeyString());

      State = order with
      {
        Operation = order.Operation with
        {
          Status = OrderStatusEnum.Position
        }
      };

      foreach (var brace in order.Orders.Where(o => o.Instruction is InstructionEnum.Brace))
      {
        var orderDescriptor = new IdentityDescriptor
        {
          Account = baseDescriptor.Account,
          Identity = order.Id
        };

        await GrainFactory.Get<IOrderGrain>(orderDescriptor).StoreOrder(brace);
      }
    }

    /// <summary>
    /// Match positions
    /// </summary>
    /// <param name="order"></param>
    public async Task<DescriptorResponse> Combine(OrderState order)
    {
      var response = new DescriptorResponse { Data = order.Id };

      if (Equals(State.Side, order.Side))
      {
        State = Increase(order);
      }
      else
      {
        var amount = State.Operation.Amount.IsGt(order.Amount) ? order.Amount : State.Operation.Amount;
        var action = Decrease(order, amount);

        if (order.Amount.Is(State.Operation.Amount))
        {
          response = response with { Data = null };
        }

        if (order.Amount.IsGt(State.Operation.Amount))
        {
          State = Reverse(order);
        }

        await orderStream.OnNextAsync(action);
      }

      await SendBraces(order);

      return response;
    }

    /// <summary>
    /// Update SL and TP orders
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    protected async Task SendBraces(OrderState order)
    {
      var converter = InstanceService<ConversionService>.Instance;
      var baseDescriptor = converter.Decompose<BaseDescriptor>(this.GetPrimaryKeyString());

      foreach (var currentOrder in State.Orders.Where(o => o.Instruction is InstructionEnum.Brace))
      {
        var orderDescriptor = new IdentityDescriptor
        {
          Account = baseDescriptor.Account,
          Identity = order.Id
        };

        await GrainFactory.Get<IOrdersGrain>(orderDescriptor).Remove(currentOrder);
      }

      foreach (var nextOrder in order.Orders.Where(o => o.Instruction is InstructionEnum.Brace))
      {
        var orderDescriptor = new IdentityDescriptor
        {
          Account = baseDescriptor.Account,
          Identity = order.Id
        };

        await GrainFactory.Get<IOrderGrain>(orderDescriptor).StoreOrder(nextOrder);
      }
    }

    /// <summary>
    /// Increase position
    /// </summary>
    /// <param name="order"></param>
    protected OrderState Increase(OrderState order)
    {
      var increaseAmount = State.Operation.Amount + order.Amount;

      return State with
      {
        Amount = increaseAmount,
        Operation = State.Operation with
        {
          Amount = increaseAmount,
          AveragePrice = GroupPrice(State, order)
        }
      };
    }

    /// <summary>
    /// Close position
    /// </summary>
    /// <param name="order"></param>
    /// <param name="amount"></param>
    protected OrderState Decrease(OrderState order, double? amount)
    {
      return State with
      {
        Amount = amount,
        Operation = State.Operation with
        {
          Amount = amount,
          Price = order.Price,
          Status = OrderStatusEnum.Transaction
        }
      };
    }

    /// <summary>
    /// Reverse position
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    protected OrderState Reverse(OrderState order)
    {
      var nextAmount = order.Amount - State.Operation.Amount;

      return order with
      {
        Amount = nextAmount,
        Operation = State.Operation with
        {
          Amount = nextAmount,
          Status = OrderStatusEnum.Position
        }
      };
    }

    /// <summary>
    /// Update instruments assigned to positions and other models
    /// </summary>
    /// <param name="point"></param>
    /// <param name="token"></param>
    protected async Task OnPrice(PriceState point, StreamSequenceToken token)
    {
      if (Equals(point.Name, State.Operation.Instrument.Name))
      {
        State = State with
        {
          Operation = State.Operation with
          {
            Instrument = State.Operation.Instrument with
            {
              Point = point
            }
          }
        };

        await Gain();
      }
    }

    /// <summary>
    /// Position direction
    /// </summary>
    /// <param name="order"></param>
    protected double? Side()
    {
      switch (State.Side)
      {
        case OrderSideEnum.Long: return 1;
        case OrderSideEnum.Short: return -1;
      }

      return null;
    }

    /// <summary>
    /// Position direction
    /// </summary>
    protected double? Amount()
    {
      var amount = State.Operation?.Amount ?? 0;
      var sideAmount = State.Orders.Sum(o => o.Operation?.Amount ?? 0);

      return amount + sideAmount;
    }

    /// <summary>
    /// Estimate open price for one of the instruments in the order
    /// </summary>
    protected double? OpenPrice()
    {
      var point = State.Operation.Instrument.Point;

      if (point is not null)
      {
        switch (State.Side)
        {
          case OrderSideEnum.Long: return point.Ask;
          case OrderSideEnum.Short: return point.Bid;
        }
      }

      return null;
    }

    /// <summary>
    /// Estimate close price for one of the instruments in the order
    /// </summary>
    protected double? ClosePrice()
    {
      var point = State.Operation.Instrument.Point;

      if (point is not null)
      {
        switch (State.Side)
        {
          case OrderSideEnum.Long: return point.Bid;
          case OrderSideEnum.Short: return point.Ask;
        }
      }

      return null;
    }

    /// <summary>
    /// Compute aggregated position price
    /// </summary>
    /// <param name="orders"></param>
    protected double? GroupPrice(params OrderState[] orders)
    {
      var numerator = 0.0 as double?;
      var denominator = 0.0 as double?;

      foreach (var o in orders)
      {
        switch (true)
        {
          case true when o.Operation.AveragePrice is null:
            numerator += o.Amount * o.Price;
            denominator += o.Amount;
            break;

          case true when o.Operation.AveragePrice is not null:
            numerator += o.Operation.Amount * o.Operation.AveragePrice;
            denominator += o.Operation.Amount;
            break;
        }
      }

      return numerator / denominator;
    }
  }
}
