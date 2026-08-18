using Core.Conventions;
using Core.Enums;
using Core.Grains;
using Core.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Topstep.Models;
using TopstepX;
using TopstepX.Models.Orders;
using TopstepX.Models.Trades;

namespace Topstep.Grains
{
  public interface ITopstepTransactionsGrain : ITransactionsGrain
  {
    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="grainObserver"></param>
    Task<StatusResponse> Setup(Connection connection, ITradeObserver grainObserver);

    /// <summary>
    /// Validate session token
    /// </summary>
    /// <param name="session"></param>
    Task<StatusResponse> Validate(string session);
  }

  public class TopstepTransactionsGrain : TransactionsGrain, ITopstepTransactionsGrain
  {
    /// <summary>
    /// State
    /// </summary>
    protected Connection state;

    /// <summary>
    /// Connector
    /// </summary>
    protected TopstepBroker connector;

    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="grainObserver"></param>
    public virtual async Task<StatusResponse> Setup(Connection connection, ITradeObserver grainObserver)
    {
      state = connection;
      observer = grainObserver;
      connector = new(connection.Username, connection.Token);

      return new()
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Validate session token
    /// </summary>
    /// <param name="session"></param>
    public virtual async Task<StatusResponse> Validate(string session)
    {
      connector.SetAuthHeader(session);

      return new()
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// Get transactions
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<OrdersResponse> Transactions(TransactionCriteria criteria)
    {
      var cts = new CancellationTokenSource(state.Timeout);
      var query = new SearchTradeRequest { accountId = int.Parse(criteria.Account.Descriptor) };
      var messages = await connector.TradeSearch(query);
      var items = messages.trades.Select(MapTransaction);

      return new()
      {
        Data = [.. items]
      };
    }

    /// <summary>
    /// Map transaction
    /// </summary>
    /// <param name="message"></param>
    protected virtual Order MapTransaction(HalfTradeModel message)
    {
      var volume = Math.Abs(message.size);
      var instrument = new Instrument
      {
        Id = message.contractId,
        Type = InstrumentEnum.Futures
      };

      var operation = new Operation
      {
        Amount = volume,
        Instrument = instrument,
        AveragePrice = message.price,
        Time = message.creationTimestamp.Ticks
      };

      var order = new Order
      {
        Amount = volume,
        Operation = operation,
        Type = OrderTypeEnum.Market,
        Price = message.price,
        Time = message.creationTimestamp.Ticks,
        Side = MapSide(message),
        Id = $"{message.id}",
        Balance = new() { Current = message.profitAndLoss }
      };

      return order;
    }

    /// <summary>
    /// Map side
    /// </summary>
    /// <param name="status"></param>
    protected virtual OrderSideEnum? MapSide(HalfTradeModel message)
    {
      switch (message.side)
      {
        case OrderSide.Buy: return OrderSideEnum.Long;
        case OrderSide.Sell: return OrderSideEnum.Short;
      }

      return null;
    }
  }
}
