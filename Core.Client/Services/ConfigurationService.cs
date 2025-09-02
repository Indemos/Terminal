using Microsoft.Extensions.Configuration;

namespace Core.Client.Services
{
  public class ConfigurationService
  {
    /// <summary>
    /// Configuration instance
    /// </summary>
    public virtual IConfiguration Setup { get; set; }
  }
}
