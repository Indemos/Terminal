using Core.Common.Services;
using Core.Common.States;
using Orleans;

namespace Core.Common.Extensions
{
  public static class GrainFactoryExtensions
  {
    /// <summary>
    /// Conversion service
    /// </summary>
    private static readonly ConversionService converter = new();

    /// <summary>
    /// Gets a grain using compound key
    /// </summary>
    public static T Get<T>(this IGrainFactory connector, IDescriptor descriptor) where T : IGrainWithStringKey
    {
      return connector.GetGrain<T>(converter.Compose(descriptor));
    }
  }
}
