using Core.Enums;
using Core.Grains;
using Core.Models;
using Schwab.Messages;
using Schwab.Models;
using Schwab.Queries;
using System;
using System.Diagnostics.Metrics;
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
    Task<StatusResponse> Setup(Connection connection);
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
    public virtual async Task<StatusResponse> Setup(Connection connection)
    {
      state = connection;
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
    public override async Task<PricesResponse> PriceGroups(Criteria criteria)
    {
      var query = new HistoryQuery()
      {
        EndDate = criteria.MaxDate.Value,
        StartDate = criteria.MinDate.Value,
        Symbol = criteria.Instrument.Name
      };

      var cleaner = new CancellationTokenSource(state.Timeout);
      var sourceResponse = await connector.GetBars(query, cleaner.Token);
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
