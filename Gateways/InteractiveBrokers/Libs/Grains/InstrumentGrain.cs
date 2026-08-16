using Core.Conventions;
using Core.Enums;
using Core.Grains;
using Core.Models;
using IBApi;
using IBApi.Queries;
using InteractiveBrokers.Mappers;
using InteractiveBrokers.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace InteractiveBrokers.Grains
{
  public interface IInterInstrumentGrain : IInstrumentGrain
  {
    /// <summary>
    /// Setup
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="grainObserver"></param>
    Task<StatusResponse> Setup(Connection connection, ITradeObserver grainObserver);
  }

  public class InterInstrumentGrain : InstrumentGrain, IInterInstrumentGrain
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
    /// Setup
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="grainObserver"></param>
    public virtual async Task<StatusResponse> Setup(Connection connection, ITradeObserver grainObserver)
    {
      state = connection;
      observer = grainObserver;

      connector?.Disconnect();
      connector = new InterBroker
      {
        Port = state.Port,
        Span = state.Span,
        Timeout = state.Timeout
      };

      await connector.Connect();

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
      var contract = Upstream.MapContract(criteria.Instrument);
      var cts = new CancellationTokenSource(state.Timeout);
      var query = new HistoricalTicksQuery()
      {
        Contract = contract,
        MinDate = criteria.MinDate.Value,
        MaxDate = criteria.MaxDate.Value,
        DataType = "BID_ASK",
        Count = criteria.Count ?? 1
      };

      var sourceItems = await connector.GetTicks(query, cts.Token);
      var items = sourceItems.Select(Downstream.MapPrice).ToList();

      await Task.Delay(state.Span);

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
      var cts = new CancellationTokenSource(state.Timeout);
      var contract = Upstream.MapContract(criteria.Instrument);
      var maxDate = criteria.MaxDate ?? DateTime.Now;
      var query = new HistoricalBarsQuery
      {
        Contract = contract,
        MaxDate = maxDate,
        BarType = criteria.FrameType,
        DataType = MapPriceType(criteria.PriceType),
        Duration = criteria.DurationType,
      };

      var sourceItems = await connector.GetBars(query, cts.Token);
      var items = sourceItems.Select(Downstream.MapPrice).ToList();

      await Task.Delay(state.Span);

      return new()
      {
        Data = items
      };
    }

    protected virtual string MapPriceType(PriceTypeEnum priceType)
    {
      switch (priceType)
      {
        case PriceTypeEnum.Trade: return "TRADES";
        case PriceTypeEnum.Tick: return "BID_ASK";
        case PriceTypeEnum.Bar: return "AGGTRADES";
      }

      return null;
    }
  }
}
