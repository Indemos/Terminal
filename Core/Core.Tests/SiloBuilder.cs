using Core.Enums;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.Extensions.Configuration;
using Orleans.Hosting;
using Orleans.Providers;
using Orleans.Serialization;
using Orleans.TestingHost;

namespace Core.Tests
{
  public class SiloConfigurator : ISiloConfigurator, IClientBuilderConfigurator
  {
    public void Configure(ISiloBuilder orleans)
    {
      orleans.AddMemoryGrainStorageAsDefault();
      orleans.AddMemoryStreams<DefaultMemoryMessageBodySerializer>(nameof(StreamEnum.Price));
      orleans.AddMemoryStreams<DefaultMemoryMessageBodySerializer>(nameof(StreamEnum.Order));
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
