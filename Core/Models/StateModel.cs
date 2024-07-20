using Terminal.Core.Enums;

namespace Terminal.Core.Models
{
  public class StateModel<T>
  {
    /// <summary>
    /// Event type
    /// </summary>
    public ActionEnum Action { get; set; }

    /// <summary>
    /// Current or next value to be set
    /// </summary>
    public T Next { get; set; }

    /// <summary>
    /// Previous value
    /// </summary>
    public T Previous { get; set; }
  }
}
