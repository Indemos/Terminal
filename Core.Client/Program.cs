using Core.Client.Services;
using Core.Common.Enums;
using Core.Common.Services;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MudBlazor;
using MudBlazor.Services;
using Orleans.Hosting;
using Orleans.Providers;
using Orleans.Serialization;

namespace Core.Client
{
  public class Program
  {
    public static void Main(string[] args)
    {
      var builder = WebApplication.CreateBuilder(args);

      builder.WebHost.UseStaticWebAssets();
      builder.UseOrleans(silo =>
      {
        silo.UseLocalhostClustering();
        silo.AddMemoryGrainStorageAsDefault();
        silo.AddMemoryStreams<DefaultMemoryMessageBodySerializer>(nameof(StreamEnum.Price));
        silo.AddMemoryStreams<DefaultMemoryMessageBodySerializer>(nameof(StreamEnum.Order));
        silo.AddMemoryGrainStorage("PubSubStore");
        silo.ConfigureServices(services =>
        {
          var messageOptions = MessagePackSerializerOptions
            .Standard
            .WithResolver(ContractlessStandardResolver.Instance);

          services.AddSingleton(messageOptions);
          services.AddSerializer(serBuilder =>
          {
            serBuilder.AddMessagePackSerializer(
              o => true,
              o => true,
              o => o.Configure(options => options.SerializerOptions = messageOptions));
          });
        });
      });

      InstanceService<ConfigurationService>.Instance.Setup = builder.Configuration;

      builder.Services.AddRazorPages();
      builder.Services.AddServerSideBlazor();
      builder.Services.AddScoped<SubscriptionService>();
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
