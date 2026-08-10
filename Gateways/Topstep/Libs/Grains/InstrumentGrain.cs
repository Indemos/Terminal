using Core.Conventions;
using Core.Enums;
using Core.Grains;
using Core.Models;
using System;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Topstep.Models;
using TopstepX;
using TopstepX.Models.History;

namespace Topstep.Grains
{
  public interface ITopstepInstrumentGrain : IInstrumentGrain
  {
    /// <summary>
    /// Connect
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="grainObserver"></param>
    Task<StatusResponse> Setup(Connection connection, ITradeObserver grainObserver);
  }

  public class TopstepInstrumentGrain : InstrumentGrain, ITopstepInstrumentGrain
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
    /// List of prices by criteria
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<PricesResponse> Prices(PriceCriteria criteria)
    {
      var query = new RetrieveBarRequest()
      {
        live = false,
        endTime = criteria.MaxDate.Value,
        startTime = criteria.MinDate.Value,
        contractId = criteria.Instrument.Id,
        unit = MapFrameType(criteria.FrameType),
        unitNumber = criteria.Frame ?? 1,
        limit = criteria.Count ?? 1
      };

      var cts = new CancellationTokenSource(state.Timeout);
      var sourceResponse = await connector.GetBars(query);
      var items = sourceResponse.bars.Select(MapPrice).ToArray();

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
      var query = new RetrieveBarRequest()
      {
        live = false,
        endTime = criteria.MaxDate.Value,
        startTime = criteria.MinDate.Value,
        contractId = criteria.Instrument.Id,
        unit = MapFrameType(criteria.FrameType),
        unitNumber = criteria.Frame ?? 1,
        limit = criteria.Count ?? 1
      };

      var cts = new CancellationTokenSource(state.Timeout);
      var sourceResponse = await connector.GetBars(query);
      var items = sourceResponse.bars.Select(MapPrice).ToArray();

      return new()
      {
        Data = items
      };
    }

    /// <summary>
    /// Map time frame
    /// </summary>
    /// <param name="span"></param>
    protected virtual AggregateBarUnit MapFrameType(string span)
    {
      switch (span?.ToUpper())
      {
        case "MINUTE": return AggregateBarUnit.Minute;
        case "HOUR": return AggregateBarUnit.Hour;
        case "DAY": return AggregateBarUnit.Day;
      }

      return AggregateBarUnit.Second;
    }

    /// <summary>
    /// Map price
    /// </summary>
    /// <param name="o"></param>
    protected virtual Price MapPrice(AggregateBarModel o) => new()
    {
      Ask = o.c,
      Bid = o.c,
      Last = o.c,
      Volume = o.v,
      Bar = new()
      {
        Open = o.o,
        High = o.h,
        Low = o.l,
        Close = o.c,
        Time = o.t.Ticks
      }
    };
  }
}
