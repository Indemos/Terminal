using Core.EnumSpace;
using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace Core.ModelSpace
{
  /// <summary>
  /// Class that can save and restore its state
  /// </summary>
  public interface IStateModel : IBaseModel
  {
    /// <summary>
    /// Current state
    /// </summary>
    StatusEnum State { get; set; }

    /// <summary>
    /// State change tracker
    /// </summary>
    ISubject<StatusEnum> StateStream { get; }

    /// <summary>
    /// Restore state and initialize
    /// </summary>
    Task Connect();

    /// <summary>
    /// Save state and dispose
    /// </summary>
    Task Disconnect();

    /// <summary>
    /// Suspend execution
    /// </summary>
    Task Unsubscribe();

    /// <summary>
    /// Continue execution
    /// </summary>
    Task Subscribe();
  }

  /// <summary>
  /// Class that can save and restore its state
  /// </summary>
  public abstract class StateModel : BaseModel, IStateModel
  {
    /// <summary>
    /// Current state
    /// </summary>
    public virtual StatusEnum State { get; set; }

    /// <summary>
    /// State change tracker
    /// </summary>
    public virtual ISubject<StatusEnum> StateStream { get; }

    /// <summary>
    /// Restore state and initialize
    /// </summary>
    public abstract Task Connect();

    /// <summary>
    /// Save state and dispose
    /// </summary>
    public abstract Task Disconnect();

    /// <summary>
    /// Suspend execution
    /// </summary>
    public abstract Task Unsubscribe();

    /// <summary>
    /// Continue execution
    /// </summary>
    public abstract Task Subscribe();

    public StateModel()
    {
      StateStream = new Subject<StatusEnum>();

      StateStream
        .TakeUntil(_subscriptions)
        .Subscribe(o => State = o);
    }
  }
}
