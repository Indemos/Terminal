using Microsoft.Extensions.Configuration;

namespace Board.Services
{
  public class ConfigurationService
  {
    /// <summary>
    /// Configuration instance
    /// </summary>
    public virtual IConfiguration Setup { get; set; }
  }
}
