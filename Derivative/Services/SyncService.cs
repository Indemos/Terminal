using Distribution.Services;
using Microsoft.Extensions.Configuration;
using Schwab;
using Schwab.Messages;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;
using Terminal.Core.Domains;
using Terminal.Core.Models;

namespace Derivative.Services
{
  public class SyncService
  {
    public Adapter Connector { get; set; }
    public IConfiguration Configuration { get; set; }
    public IList<string> Assets { get; set; } = [];
    public IDictionary<string, IList<InstrumentModel>> Options { get; set; } = new ConcurrentDictionary<string, IList<InstrumentModel>>();

    public SyncService(IConfiguration configuration)
    {
      Configuration = configuration;
      Assets = ["SPY", "AAPL", "MSFT", "GOOG", "TSLA", "NVDA", "AMZN", "META"];
    }

    public async Task Connect()
    {
      Connector = new Adapter
      {
        Account = new Account
        {
          Descriptor = Configuration.GetValue<string>("Schwab:Account")
        },
        Scope = new ScopeMessage
        {
          ConsumerKey = Configuration.GetValue<string>("Schwab:ConsumerKey"),
          ConsumerSecret = Configuration.GetValue<string>("Schwab:ConsumerSecret"),
          RefreshToken = Configuration.GetValue<string>("Schwab:RefreshToken"),
          AccessToken = Configuration.GetValue<string>("Schwab:AccessToken")
        }
      };

      var interval = new Timer(TimeSpan.FromMinutes(1));
      var scheduler = InstanceService<ScheduleService>.Instance;

      await Connector.Connect();

      scheduler.Send(() => Update(Assets));

      interval.Enabled = true;
      interval.Elapsed += (sender, e) => scheduler.Send(() => Update(Assets));
    }

    protected async Task Update(IList<string> assets)
    {
      foreach (var asset in assets)
      {
        var date = DateTime.Now;
        var options = await Connector.GetOptions(new OptionScreenerModel(), new Hashtable
        {
          ["strikeCount"] = 50,
          ["symbol"] = asset.ToUpper(),
          ["fromDate"] = $"{date:yyyy-MM-dd}",
          ["toDate"] = $"{date.AddYears(1):yyyy-MM-dd}"
        });

        Options[asset] = options.Data;
      }
    }
  }
}
