//using Core.Conventions;
//using Core.Enums;
//using Core.Grains;
//using Core.Models;
//using Tradier.Grains;
//using Tradier.Models;
//using System.Threading.Tasks;

//namespace Tradier
//{
//  public class TradierGateway : Gateway
//  {
//    /// <summary>
//    /// Access token
//    /// </summary>
//    public virtual string AccessToken { get; set; }

//    /// <summary>
//    /// Connect
//    /// </summary>
//    public override async Task<StatusResponse> Connect()
//    {
//      var connection = new Connection()
//      {
//        Account = Account
//      };

//      SubscribeToUpdates();

//      await Component<ITradierOrdersGrain>().Setup(connection);
//      await Component<ITradierOptionsGrain>().Setup(connection);
//      await Component<ITradierPositionsGrain>().Setup(connection);
//      await Component<ITradierConnectionGrain>().Setup(connection);
//      await Component<ITradierOrderSenderGrain>().Setup(connection);
//      await Component<ITradierTransactionsGrain>().Setup(connection);

//      return new()
//      {
//        Data = StatusEnum.Active
//      };
//    }

//    /// <summary>
//    /// Save state and dispose
//    /// </summary>
//    public override Task<StatusResponse> Disconnect()
//    {
//      return Component<ITradierConnectionGrain>().Disconnect();
//    }

//    /// <summary>
//    /// Subscribe to streams
//    /// </summary>
//    /// <param name="instrument"></param>
//    public override Task<StatusResponse> Subscribe(Instrument instrument)
//    {
//      return Component<ITradierConnectionGrain>().Subscribe(instrument);
//    }

//    /// <summary>
//    /// Unsubscribe from streams
//    /// </summary>
//    /// <param name="instrument"></param>
//    public override Task<StatusResponse> Unsubscribe(Instrument instrument)
//    {
//      return Task.FromResult(new StatusResponse { Data = StatusEnum.Pause });
//    }

//    /// <summary>
//    /// Get depth of market when available or just a top of the book
//    /// </summary>
//    /// <param name="criteria"></param>
//    public override Task<DomResponse> GetDom(Criteria criteria)
//    {
//      return Component<IDomGrain>(criteria.Instrument.Name).Dom(criteria);
//    }

//    /// <summary>
//    /// Ticks
//    /// </summary>
//    /// <param name="criteria"></param>
//    public override Task<PricesResponse> GetPrices(Criteria criteria)
//    {
//      return Component<IInstrumentGrain>(criteria.Instrument.Name).Prices(criteria);
//    }

//    /// <summary>
//    /// Bars
//    /// </summary>
//    /// <param name="criteria"></param>
//    public override Task<PricesResponse> GetPriceGroups(Criteria criteria)
//    {
//      return Component<IInstrumentGrain>(criteria.Instrument.Name).PriceGroups(criteria);
//    }

//    /// <summary>
//    /// Option chain
//    /// </summary>
//    /// <param name="criteria"></param>
//    public override Task<InstrumentsResponse> GetOptions(Criteria criteria)
//    {
//      return Component<ITradierOptionsGrain>(criteria.Instrument.Name).Options(criteria);
//    }

//    /// <summary>
//    /// Get all account orders
//    /// </summary>
//    /// <param name="criteria"></param>
//    public override Task<OrdersResponse> GetOrders(Criteria criteria)
//    {
//      return Component<ITradierOrdersGrain>().Orders(criteria with { Account = Account });
//    }

//    /// <summary>
//    /// Get all account positions
//    /// </summary>
//    /// <param name="criteria"></param>
//    public override Task<OrdersResponse> GetPositions(Criteria criteria)
//    {
//      return Component<ITradierPositionsGrain>().Positions(criteria);
//    }

//    /// <summary>
//    /// Get all account transactions
//    /// </summary>
//    /// <param name="criteria"></param>
//    public override Task<OrdersResponse> GetTransactions(Criteria criteria)
//    {
//      return Component<ITradierTransactionsGrain>().Transactions(criteria);
//    }

//    /// <summary>
//    /// Create order and depending on the account, send it to the processing queue
//    /// </summary>
//    /// <param name="order"></param>
//    public override Task<OrderResponse> SendOrder(Order order)
//    {
//      return Component<ITradierOrderSenderGrain>().Send(order);
//    }

//    /// <summary>
//    /// Clear order
//    /// </summary>
//    /// <param name="order"></param>
//    public override Task<DescriptorResponse> ClearOrder(Order order)
//    {
//      return Component<ITradierOrderSenderGrain>().Clear(order);
//    }
//  }
//}
