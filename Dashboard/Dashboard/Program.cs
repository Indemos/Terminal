using Core.Models;
using Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MudBlazor;
using MudBlazor.Services;
using Orleans.Configuration;
using Orleans;
using Orleans.Hosting;
using Orleans.Providers;
using Orleans.Serialization;
using System;

namespace Dashboard
{
  public class Program
  {
    public static void Main(string[] args)
    {
      var builder = WebApplication.CreateBuilder(args);
      var setup = builder.Configuration;

      builder.WebHost.UseUrls(setup.GetValue<string>("Apps:Address"));
      builder.Host.UseOrleans((o, orleans) =>
      {
        orleans.UseLocalhostClustering();
        orleans.AddMemoryGrainStorageAsDefault();
        orleans.AddMemoryStreams<DefaultMemoryMessageBodySerializer>(nameof(Message));
        orleans.AddMemoryGrainStorage("PubSubStore");
        orleans.UseDashboard(options =>
        {
          options.Port = setup.GetValue<int>("Apps:Dashboard:Port");
          options.Host = setup.GetValue<string>("Apps:Dashboard:Host");
        });

        orleans.Configure<GrainCollectionOptions>(options =>
        {
          options.CollectionAge = TimeSpan.FromDays(100);
          options.CollectionQuantum = TimeSpan.FromDays(1);
          options.DeactivationTimeout = TimeSpan.FromDays(1);
        });

        orleans.ConfigureServices(services =>
        {
          var converter = new ConversionService();

          services.AddSingleton(converter.MessageOptions);
          services.AddSerializer(serBuilder => serBuilder.AddMessagePackSerializer(
            o => true,
            o => true,
            o => o.Configure(options => options.SerializerOptions = converter.MessageOptions)));
        });
      });

      builder.WebHost.UseStaticWebAssets();
      builder.Services.AddRazorPages();
      builder.Services.AddServerSideBlazor();
      builder.Services.AddSingleton<StateService>();
      builder.Services.AddSingleton<SchedulerService>();
      builder.Services.AddSingleton(o => new LogService(setup["Documents:Logs"]));
      builder.Services.AddMudServices(o =>
      {
        o.SnackbarConfiguration.NewestOnTop = true;
        o.SnackbarConfiguration.ShowCloseIcon = true;
        o.SnackbarConfiguration.PreventDuplicates = true;
        o.SnackbarConfiguration.SnackbarVariant = Variant.Outlined;
        o.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.TopRight;
      });

      var app = builder.Build();

      app.UseStaticFiles();
      app.UseRouting();
      app.MapBlazorHub();
      app.MapFallbackToPage("/Host");
      app.Run();
    }
  }
}
