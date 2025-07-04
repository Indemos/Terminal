using Distribution.Services;
using Distribution.Stream;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Schwab;
using Schwab.Enums;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;
using Terminal.Components;
using Terminal.Core.Collections;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Extensions;
using Terminal.Core.Models;
using Terminal.Core.Services;
using Terminal.Services;

namespace Terminal.Pages.Utils
{
  public partial class Ticks
  {
    [Inject] IConfiguration Configuration { get; set; }

    protected virtual ControlsComponent View { get; set; }
    protected virtual Service Srv { get; set; } = new Service();

    protected override async Task OnAfterRenderAsync(bool setup)
    {
      if (setup)
      {
        InstanceService<SubscriptionService>.Instance.Update += state =>
        {
          switch (true)
          {
            case true when state.Previous is SubscriptionEnum.None && state.Next is SubscriptionEnum.Progress: CreateAccounts(); break;
          }
        };
      }

      await base.OnAfterRenderAsync(setup);
    }

    protected virtual void CreateAccounts()
    {
      var account = new Account
      {
        Descriptor = Configuration["Schwab:Account"],
        States = new Map<string, SummaryModel>
        {
          ["/ESU25"] = new SummaryModel
          {
            Instrument = new InstrumentModel
            {
              Name = "/ESU25",
              Type = InstrumentEnum.Futures,
            }
          },
          ["/NQU25"] = new SummaryModel
          {
            Instrument = new InstrumentModel
            {
              Name = "/NQU25",
              Type = InstrumentEnum.Futures,
            }
          },
          ["/YMU25"] = new SummaryModel
          {
            Instrument = new InstrumentModel
            {
              Name = "/YMU25",
              Type = InstrumentEnum.Futures,
            }
          },
          ["/CLN25"] = new SummaryModel
          {
            Instrument = new InstrumentModel
            {
              Name = "/CLN25",
              Type = InstrumentEnum.Futures,
            }
          },
          ["/6EU25"] = new SummaryModel
          {
            Instrument = new InstrumentModel
            {
              Name = "/6EU25",
              Type = InstrumentEnum.Futures,
            }
          },
          ["/6CU25"] = new SummaryModel
          {
            Instrument = new InstrumentModel
            {
              Name = "/6CU25",
              Type = InstrumentEnum.Futures,
            }
          },
          ["/6SU25"] = new SummaryModel
          {
            Instrument = new InstrumentModel
            {
              Name = "/6SU25",
              Type = InstrumentEnum.Futures,
            }
          },
          ["/6AU25"] = new SummaryModel
          {
            Instrument = new InstrumentModel
            {
              Name = "/6AU25",
              Type = InstrumentEnum.Futures,
            }
          },
          ["/6BU25"] = new SummaryModel
          {
            Instrument = new InstrumentModel
            {
              Name = "/6BU25",
              Type = InstrumentEnum.Futures,
            }
          },
          ["/6JU25"] = new SummaryModel
          {
            Instrument = new InstrumentModel
            {
              Name = "/6JU25",
              Type = InstrumentEnum.Futures,
            }
          },
          ["/GCQ25"] = new SummaryModel
          {
            Instrument = new InstrumentModel
            {
              Name = "/GCQ25",
              Type = InstrumentEnum.Futures,
            }
          },
          ["/ZBU25"] = new SummaryModel
          {
            Instrument = new InstrumentModel
            {
              Name = "/ZBU25",
              Type = InstrumentEnum.Futures,
            }
          },
          ["/ZNU25"] = new SummaryModel
          {
            Instrument = new InstrumentModel
            {
              Name = "/ZNU25",
              Type = InstrumentEnum.Futures,
            }
          },
          ["/ZQU25"] = new SummaryModel
          {
            Instrument = new InstrumentModel
            {
              Name = "/ZQU25",
              Type = InstrumentEnum.Futures,
            }
          },
          ["/BTCU25"] = new SummaryModel
          {
            Instrument = new InstrumentModel
            {
              Name = "/BTCU25",
              Type = InstrumentEnum.Futures,
            }
          },
          ["SPY"] = new SummaryModel
          {
            Instrument = new InstrumentModel
            {
              Name = "SPY",
              Type = InstrumentEnum.Shares,
            }
          },
        }
      };

      var adapter = View.Adapters["Prime"] = new Adapter
      {
        Account = account,
        AccessToken = Configuration["Schwab:AccessToken"],
        RefreshToken = Configuration["Schwab:RefreshToken"],
        ClientId = Configuration["Schwab:ConsumerKey"],
        ClientSecret = Configuration["Schwab:ConsumerSecret"]
      };

      View
        .Adapters
        .Values
        .ForEach(adapter => adapter.Stream += message =>
        {
          var date = $"{DateTime.Now:yyyy-MM-dd}";
          var asset = message.Next.Instrument.Name;
          var storage = $"D:/Code/NET/Terminal/Data/Series/{date}/{asset}";
          var summary = adapter.Account.States.Get(asset);

          summary.Points.Clear();
          summary.PointGroups.Clear();

          var content = JsonSerializer.Serialize(summary, Srv.Options);
          var source = $"{storage}/{DateTime.UtcNow.Ticks}.zip";

          Directory.CreateDirectory(storage);

          using var archive = ZipFile.Open(source, ZipArchiveMode.Create);
          using (var entry = archive.CreateEntry($"{DateTime.UtcNow.Ticks}").Open())
          {
            var bytes = Encoding.UTF8.GetBytes(content);
            entry.Write(bytes);
          }
        });
    }
  }
}
