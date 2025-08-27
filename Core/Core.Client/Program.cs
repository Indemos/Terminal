using Core.Client.Services;
using Core.Common.Enums;
using Core.Common.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MudBlazor;
using MudBlazor.Services;
using Orleans.Hosting;
using Orleans.Providers;

namespace Core.Client
{
  public class Program
  {
    public static void Main(string[] args)
    {
      var builder = WebApplication.CreateBuilder(args);

      builder.WebHost.UseStaticWebAssets();
      builder.Host.UseOrleans((ctx, o) =>
      {
        o.UseLocalhostClustering();
        o.AddMemoryGrainStorageAsDefault();
        o.AddMemoryStreams<DefaultMemoryMessageBodySerializer>(nameof(StreamEnum.Price));
        o.AddMemoryStreams<DefaultMemoryMessageBodySerializer>(nameof(StreamEnum.Order));
        o.AddMemoryGrainStorage("MemStore");
      });

      InstanceService<ConfigurationService>.Instance.Setup = builder.Configuration;

      builder.Services.AddRazorPages();
      builder.Services.AddServerSideBlazor();
      builder.Services.AddMudServices(o =>
      {
        o.SnackbarConfiguration.NewestOnTop = true;
        o.SnackbarConfiguration.ShowCloseIcon = true;
        o.SnackbarConfiguration.PreventDuplicates = true;
        o.SnackbarConfiguration.SnackbarVariant = Variant.Outlined;
        o.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.TopRight;
      });

      var app = builder.Build();

      app.UseAntiforgery();
      app.UseStaticFiles();
      app.UseRouting();
      app.MapBlazorHub();
      app.MapFallbackToPage("/Host");
      app.Run();
    }
  }
}
