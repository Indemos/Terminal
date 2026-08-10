using Core.Conventions;
using Core.Enums;
using Core.Extensions;
using Core.Grains;
using Core.Models;
using IBApi;
using IBApi.Messages;
using InteractiveBrokers.Mappers;
using InteractiveBrokers.Models;
using Orleans;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace InteractiveBrokers.Grains
{
  public interface IInterConnectionGrain : IConnectionGrain
  {
    /// <summary>
    /// Setup
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="grainObserver"></param>
    Task<StatusResponse> Setup(Connection connection, ITradeObserver grainObserver);

    /// <summary>
    /// Sync open balance, order, and positions 
    /// </summary>
    Task<Account> AccountSummary();
  }

  public class InterConnectionGrain : ConnectionGrain, IInterConnectionGrain
  {
    /// <summary>
    /// State
    /// </summary>
    protected Connection state;

    /// <summary>
    /// IB client
    /// </summary>
    protected InterBroker connector;

    /// <summary>
    /// Asset subscriptions
    /// </summary>
    protected ConcurrentDictionary<string, int> subscriptions = new();

    /// <summary>
    /// Deactivation
    /// </summary>
    /// <param name="reason"></param>
    /// <param name="cts"></param>
    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cts)
    {
      await Disconnect();
      await base.OnActivateAsync(cts);
    }

    /// <summary>
    /// Setup
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="grainObserver"></param>
    public virtual async Task<StatusResponse> Setup(Connection connection, ITradeObserver grainObserver)
    {
      state = connection;
      observer = grainObserver;
      connector = new InterBroker
      {
        Port = state.Port,
        Span = state.Span,
        Timeout = state.Timeout
      };

      var id = await connector.Connect();

      if (id is 0)
      {
        await messenger.OnNextAsync(new Message()
        {
          Description = "No connection",
          Action = ActionEnum.Disconnect
        });

        return new()
        {
          Data = StatusEnum.Inactive
        };
      }

      var descriptor = this.GetDescriptor();
      var ordersGrain = GrainFactory.GetGrain<IInterOrdersGrain>(descriptor);
      var positionsGrain = GrainFactory.GetGrain<IInterPositionsGrain>(descriptor);
      var orderSenderGrain = GrainFactory.GetGrain<IInterOrderSenderGrain>(descriptor);
      var transactionsGrain = GrainFactory.GetGrain<IInterTransactionsGrain>(descriptor);

      await ordersGrain.Setup(connection, observer);
      await positionsGrain.Setup(connection, observer);
      await orderSenderGrain.Setup(connection, observer);
      await transactionsGrain.Setup(connection, observer);

      foreach (var instrument in state.Account.Instruments.Values)
      {
        await Subscribe(instrument);
      }

      return new()
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public override Task<StatusResponse> Disconnect()
    {
      connector?.Disconnect();

      return Task.FromResult(new StatusResponse()
      {
        Data = StatusEnum.Inactive
      });
    }

    /// <summary>
    /// Subscribe to streams
    /// </summary>
    /// <param name="instrument"></param>
    public override async Task<StatusResponse> Subscribe(Instrument instrument)
    {
      await Unsubscribe(instrument);

      var contract = Upstream.MapContract(instrument);
      var cts = new CancellationTokenSource(state.Timeout);
      var contracts = await connector.GetContracts(contract, cts.Token);
      var contractMessage = contracts.FirstOrDefault();

      if (contractMessage is null)
      {
        await messenger.OnNextAsync(new Message()
        {
          Description = "No such instrument",
          Action = ActionEnum.Disconnect
        });

        return new()
        {
          Data = StatusEnum.Inactive
        };
      }

      var name = this.GetDescriptor();
      var instrumentGrain = GrainFactory.GetGrain<IInstrumentGrain>(this.GetDescriptor(instrument.Name));
      var dataMessage = new PriceStreamMessage
      {
        DataTypes = [IBApi.Enums.SubscriptionEnum.Price],
        Contract = contract
      };

      subscriptions[instrument.Name] = connector.SubscribeToTicks(dataMessage, async priceMessage =>
      {
        var price = Downstream.MapPrice(priceMessage);
        var group = await instrumentGrain.Send(instrument with { Price = price });

        await observer.StreamInstrument(group);
      });

      return new()
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Unsubscribe from streams
    /// </summary>
    /// <param name="instrument"></param>
    public override Task<StatusResponse> Unsubscribe(Instrument instrument)
    {
      if (subscriptions.TryRemove(instrument.Name, out var subscription))
      {
        connector.Unsubscribe(subscription);
      }

      return Task.FromResult(new StatusResponse
      {
        Data = StatusEnum.Pause
      });
    }

    /// <summary>
    /// Sync open balance, order, and positions 
    /// </summary>
    public virtual async Task<Account> AccountSummary()
    {
      var account = new Account();
      var cts = new CancellationTokenSource(state.Timeout);
      var message = await connector.GetAccountSummary(cts.Token);

      account = account with { Balance = double.Parse(message.Get("NetLiquidation")) };

      await Task.Delay(state.Span);

      return account;
    }
  }
}
