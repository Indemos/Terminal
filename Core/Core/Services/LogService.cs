using Serilog;

namespace Core.Services
{
  public class LogService
  {
    /// <summary>
    /// Logger instance
    /// </summary>
    public virtual ILogger Data => Log.Logger;

    /// <summary>
    /// Constructor
    /// </summary>
    public LogService(string source) => Log.Logger = new LoggerConfiguration()
      .MinimumLevel.Debug()
      .Enrich.FromLogContext()
      .WriteTo.File(source)
      .CreateLogger();
  }
}
