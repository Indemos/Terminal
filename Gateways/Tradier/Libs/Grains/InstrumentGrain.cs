using Core.Conventions;
using Core.Enums;
using Core.Grains;
using Core.Models;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tradier.Messages.MarketData;
using Tradier.Models;
using Tradier.Queries.MarketData;

namespace Tradier.Grains
{
  public interface ITradierInstrumentGrain : IInstrumentGrain
  {
    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="grainObserver"></param>
    Task<StatusResponse> Setup(Connection connection, ITradeObserver grainObserver);
  }

  public class TradierInstrumentGrain : InstrumentGrain, ITradierInstrumentGrain
  {
    /// <summary>
    /// State
    /// </summary>
    protected Connection state;

    /// <summary>
    /// Connector
    /// </summary>
    protected TradierBroker connector = new();

    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="grainObserver"></param>
    public virtual async Task<StatusResponse> Setup(Connection connection, ITradeObserver grainObserver)
    {
      state = connection;
      observer = grainObserver;
      connector = new()
      {
        Token = connection.AccessToken,
        SessionToken = connection.SessionToken,
      };

      return new()
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<PricesResponse> Prices(PriceCriteria criteria)
    {
      var query = new TimeSalesRequest()
      {
        Filter = "all",
        End = criteria.MaxDate.Value,
        Start = criteria.MinDate.Value,
        Symbol = criteria.Instrument.Name,
        Interval = "tick"
      };

      var cts = new CancellationTokenSource(state.Timeout);
      var sourceResponse = await connector.GetTimeSales(query, cts.Token);
      var items = sourceResponse.Items.Select(MapPrice).ToArray();

      return new()
      {
        Data = items
      };
    }

    /// <summary>
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<PricesResponse> PriceGroups(PriceCriteria criteria)
    {
      var query = new TimeSalesRequest()
      {
        Filter = "all",
        End = criteria.MaxDate.Value,
        Start = criteria.MinDate.Value,
        Symbol = criteria.Instrument.Name,
        Interval = criteria.Frame + criteria.FrameType
      };

      var cts = new CancellationTokenSource(state.Timeout);
      var sourceResponse = await connector.GetTimeSales(query, cts.Token);
      var items = sourceResponse.Items.Select(MapPrice).ToArray();

      return new()
      {
        Data = items
      };
    }

    /// <summary>
    /// Map price
    /// </summary>
    /// <param name="o"></param>
    protected virtual Price MapPrice(DatumMessage o) => new()
    {
      Ask = o.Close,
      Bid = o.Close,
      Last = o.Close,
      Volume = o.Volume,
      Bar = new()
      {
        Open = o.Open,
        High = o.High,
        Low = o.Low,
        Close = o.Close,
        Time = o.Timestamp?.Ticks
      }
    };
  }
}
