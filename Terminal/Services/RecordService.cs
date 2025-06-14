using Distribution.Services;
using Serilog;

namespace Terminal.Services
{
  public class RecordService
  {
    /// <summary>
    /// Logger instance
    /// </summary>
    public virtual ILogger Recorder => Log.Logger;

    /// <summary>
    /// Constructor
    /// </summary>
    public RecordService()
    {
      var setup = InstanceService<ConfigurationService>.Instance.Setup;

      Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .Enrich.FromLogContext()
        .WriteTo.File($"{setup["Logs:Source"]}")
        .CreateLogger();
    }
  }
}
