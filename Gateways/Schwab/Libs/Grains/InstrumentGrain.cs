using Core.Conventions;
using Core.Enums;
using Core.Grains;
using Core.Models;
using Schwab.Messages;
using Schwab.Models;
using Schwab.Queries;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Schwab.Grains
{
  public interface ISchwabInstrumentGrain : IInstrumentGrain
  {
    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="grainObserver"></param>
    Task<StatusResponse> Setup(Connection connection, ITradeObserver grainObserver);
  }

  public class SchwabInstrumentGrain : InstrumentGrain, ISchwabInstrumentGrain
  {
    /// <summary>
    /// State
    /// </summary>
    protected Connection state;

    /// <summary>
    /// Connector
    /// </summary>
    protected SchwabBroker connector = new();

    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="grainObserver"></param>
    public virtual async Task<StatusResponse> Setup(Connection connection, ITradeObserver grainObserver)
    {
      state = connection;
      observer = grainObserver;
      connector.AccessToken = connection.AccessToken;

      return new()
      {
        Data = StatusEnum.Active
      };
    }

    /// <summary>
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<PricesResponse> PriceGroups(PriceCriteria criteria)
    {
      var query = new HistoryQuery()
      {
        EndDate = criteria.MaxDate.Value,
        StartDate = criteria.MinDate.Value,
        Symbol = criteria.Instrument.Name
      };

      var cts = new CancellationTokenSource(state.Timeout);
      var sourceResponse = await connector.GetBars(query, cts.Token);
      var items = sourceResponse.Bars.Select(MapPrice).ToArray();

      return new()
      {
        Data = items
      };
    }

    /// <summary>
    /// Map price
    /// </summary>
    /// <param name="o"></param>
    protected virtual Price MapPrice(BarMessage o) => new()
    {
      AskSize = 0,
      BidSize = 0,
      Ask = o.Close,
      Bid = o.Close,
      Last = o.Close,
      Volume = o.Volume,
      Time = o.Datetime
    };
  }
}
