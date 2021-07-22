using System.Reactive.Concurrency;

namespace Core.ModelSpace
{
  /// <summary>
  /// Definition
  /// </summary>
  public interface IScheduleService
  {
    /// <summary>
    /// Single instance
    /// </summary>
    public EventLoopScheduler Scheduler { get; }
  }

  /// <summary>
  /// Service to track account changes, including equity and quotes
  /// </summary>
  public class ScheduleService : IScheduleService
  {
    /// <summary>
    /// Logger instance
    /// </summary>
    public EventLoopScheduler Scheduler { get; }

    /// <summary>
    /// Constructor
    /// </summary>
    public ScheduleService() => Scheduler = new EventLoopScheduler();
  }
}
