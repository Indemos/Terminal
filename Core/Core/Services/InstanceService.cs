namespace Core.Services
{
  /// <summary>
  /// Service to track account changes, including equity and quotes
  /// </summary>
  public class InstanceService<T> where T : new()
  {
    private static readonly T instance = new T();

    /// <summary>
    /// Single instance
    /// </summary>
    public static T Instance => instance;

    /// <summary>
    /// Constructor
    /// </summary>
    static InstanceService()
    {
    }

    /// <summary>
    /// Constructor
    /// </summary>
    private InstanceService()
    {
    }
  }
}
