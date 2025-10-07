using Microsoft.Extensions.Configuration;
using Serilog;

namespace Board.Services
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
    public LogService(IConfiguration setup) => Log.Logger = new LoggerConfiguration()
      .MinimumLevel.Debug()
      .Enrich.FromLogContext()
      .WriteTo.File($"{setup["Logs:Source"]}")
      .CreateLogger();
  }
}
