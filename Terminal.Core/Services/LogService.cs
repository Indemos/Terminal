using Serilog;

namespace Terminal.Core.ServiceSpace
{
  /// <summary>
  /// Definition
  /// </summary>
  public interface ILogService
  {
    /// <summary>
    /// Single instance
    /// </summary>
    public ILogger Log { get; }
  }

  /// <summary>
  /// Service to track account changes, including equity and quotes
  /// </summary>
  public class LogService : ILogService
  {
    /// <summary>
    /// Logger instance
    /// </summary>
    public virtual ILogger Log => Serilog.Log.Logger;

    /// <summary>
    /// Constructor
    /// </summary>
    public LogService() => Serilog.Log.Logger = new LoggerConfiguration()
      .MinimumLevel.Debug()
      .Enrich.FromLogContext()
      .WriteTo.Debug()
      .CreateLogger();
  }
}
