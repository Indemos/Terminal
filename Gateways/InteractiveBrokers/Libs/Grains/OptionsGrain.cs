using Core.Conventions;
using Core.Enums;
using Core.Grains;
using Core.Models;
using IBApi;
using InteractiveBrokers.Mappers;
using InteractiveBrokers.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace InteractiveBrokers.Grains
{
  public interface IInterOptionsGrain : IOptionsGrain
  {
    /// <summary>
    /// Setup
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="grainObserver"></param>
    Task<StatusResponse> Setup(Connection connection, ITradeObserver grainObserver);
  }

  public class InterOptionsGrain : OptionsGrain, IInterOptionsGrain
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
    /// List options
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<InstrumentsResponse> Options(OptionCriteria criteria)
    {
      var instrument = criteria.Instrument;
      var minDate = criteria.MinDate?.ToString($"yyyyMMdd-HH:mm:ss");
      var maxDate = (criteria.MaxDate ?? DateTime.Now).ToString($"yyyyMMdd-HH:mm:ss");
      var contract = Upstream.MapContract(criteria.Instrument);
      var cts = new CancellationTokenSource(state.Timeout);
      var sourceItems = await connector.GetContracts(contract, cts.Token);
      var items = sourceItems.Select(o => Downstream.MapInstrumentType(o.Contract)).ToList();

      await Task.Delay(state.Span);

      return new()
      {
        Data = items
      };
    }
  }
}
