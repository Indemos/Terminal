using Board.Services;
using Core.Enums;
using Core.Services;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MudBlazor;
using MudBlazor.Services;
using Orleans;
using Orleans.Hosting;
using Orleans.Providers;
using Orleans.Serialization;

namespace Board
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
        orleans.AddMemoryStreams<DefaultMemoryMessageBodySerializer>(nameof(StreamEnum.Price));
        orleans.AddMemoryStreams<DefaultMemoryMessageBodySerializer>(nameof(StreamEnum.Order));
        orleans.AddMemoryStreams<DefaultMemoryMessageBodySerializer>(nameof(StreamEnum.Message));
        orleans.AddMemoryGrainStorage("PubSubStore");
        orleans.UseDashboard(options =>
        {
          options.Port = setup.GetValue<int>("Apps:Dashboard:Port");
          options.Host = setup.GetValue<string>("Apps:Dashboard:Host");
        });

        orleans.ConfigureServices(services =>
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

      builder.WebHost.UseStaticWebAssets();
      builder.Services.AddRazorPages();
      builder.Services.AddServerSideBlazor();
      builder.Services.AddScoped<CommunicationService>();
      builder.Services.AddScoped(o => new RecordService(setup["Documents:Logs"]));
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
