using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Core.ModelSpace;
using Terminal.Core.ServiceSpace;

namespace Terminal.Connector.Alpaca
{
  public class Adapter : ConnectorModel, IDisposable
  {
    /// <summary>
    /// API key
    /// </summary>
    public string Token { get; set; }

    /// <summary>
    /// API secret
    /// </summary>
    public string Secret { get; set; }

    /// <summary>
    /// Data source
    /// </summary>
    public string DataSource { get; set; } = "https://data.alpaca.markets";

    /// <summary>
    /// Data source
    /// </summary>
    public string QuerySource { get; set; } = "https://api.alpaca.markets";

    /// <summary>
    /// Stream source
    /// </summary>
    public string StreamSource { get; set; } = "wss://data.alpaca.markets/stream";

    /// <summary>
    /// Establish connection with a server
    /// </summary>
    /// <param name="docHeader"></param>
    public override Task Connect()
    {
      return Task.Run(async () =>
      {
        try
        {
          await Disconnect();
          await Subscribe();
        }
        catch (Exception e)
        {
          IInstanceManager<LogService>.Instance.Log.Error(e.ToString());
        }
      });
    }

    /// <summary>
    /// Dispose environment
    /// </summary>
    /// <returns></returns>
    public override Task Disconnect()
    {
      Unsubscribe();

      return Task.FromResult(0);
    }

    /// <summary>
    /// Start streaming
    /// </summary>
    /// <returns></returns>
    public override async Task Subscribe()
    {
      await Unsubscribe();
    }

    /// <summary>
    /// Stop streaming without but keep environment state
    /// </summary>
    /// <returns></returns>
    public override Task Unsubscribe()
    {
      return Task.FromResult(0);
    }
  }
}
