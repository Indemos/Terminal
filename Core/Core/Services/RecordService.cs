using Serilog;

namespace Core.Services
{
  public class RecordService
  {
    /// <summary>
    /// Logger instance
    /// </summary>
    public virtual ILogger Service => Log.Logger;

    /// <summary>
    /// Constructor
    /// </summary>
    public RecordService(string source)
    {
      Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .Enrich.FromLogContext()
        .WriteTo.File(source)
        .CreateLogger();
    }
  }
}
