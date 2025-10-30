using MessagePack;
using MessagePack.Resolvers;
using Microsoft.Extensions.Configuration;
using Orleans.Hosting;
using Orleans.Serialization;
using Orleans.TestingHost;

namespace Core.Tests
{
  public class SiloConfigurator : ISiloConfigurator, IClientBuilderConfigurator
  {
    public void Configure(ISiloBuilder orleans)
    {
      orleans.AddMemoryGrainStorageAsDefault();
      orleans.AddMemoryGrainStorage("PubSubStore");
      orleans.Services.AddSerializer(serializers =>
      {
        var messageOptions = MessagePackSerializerOptions
          .Standard
          .WithResolver(ContractlessStandardResolver.Instance);

        serializers.AddMessagePackSerializer(
          o => true,
          o => true,
          o => o.Configure(options => options.SerializerOptions = messageOptions));
      });
    }

    public void Configure(IConfiguration configuration, IClientBuilder orleans)
    {
      orleans.Services.AddSerializer(serializers =>
      {
        var messageOptions = MessagePackSerializerOptions
          .Standard
          .WithResolver(ContractlessStandardResolver.Instance);

        serializers.AddMessagePackSerializer(
          o => true,
          o => true,
          o => o.Configure(options => options.SerializerOptions = messageOptions));
      });
    }
  }
}
