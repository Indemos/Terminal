using Orleans;
using Orleans.Runtime;

namespace Core.Extensions
{
  public static class GrainExtensions
  {
    /// <summary>
    /// Naming convention
    /// </summary>
    /// <param name="grain"></param>
    public static string GetDescriptor(this IAddressable grain)
    {
      return grain.GetPrimaryKeyString();
    }

    /// <summary>
    /// Naming convention
    /// </summary>
    /// <param name="grain"></param>
    /// <param name="name"></param>
    public static string GetDescriptor(this IAddressable grain, string name)
    {
      return $"{grain.GetPrimaryKeyString()}:{name}";
    }
  }
}
