using Core.Conventions;
using Core.Enums;
using Core.Grains;
using Core.Models;
using IBApi;
using InteractiveBrokers.Mappers;
using InteractiveBrokers.Models;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace InteractiveBrokers.Grains
{
  public interface IInterPositionsGrain : IPositionsGrain
  {
    /// <summary>
    /// Setup
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="grainObserver"></param>
    Task<StatusResponse> Setup(Connection connection, ITradeObserver grainObserver);
  }

  public class InterPositionsGrain : PositionsGrain, IInterPositionsGrain
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
    /// Get positions 
    /// </summary>
    /// <param name="criteria"></param>
    public override async Task<OrdersResponse> Positions(PositionCriteria criteria)
    {
      var cts = new CancellationTokenSource(state.Timeout);
      var sourceItems = await connector.GetPositions(state.Account.Descriptor, cts.Token);
      var items = sourceItems.Where(o => o.Position is not 0).Select(Downstream.MapPosition).ToArray();

      await Task.Delay(state.Span);

      return new()
      {
        Data = items
      };
    }
  }
}
