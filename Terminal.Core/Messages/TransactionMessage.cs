using Terminal.Core.EnumSpace;

namespace Terminal.Core.MessageSpace
{
  /// <summary>
  /// Definition
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public interface ITransactionMessage<T> : IBaseMessage
  {
    /// <summary>
    /// Event type, e.g. CRUD
    /// </summary>
    ActionEnum Action { get; set; }

    /// <summary>
    /// Current or next value to be set
    /// </summary>
    T Next { get; set; }

    /// <summary>
    /// Previous value
    /// </summary>
    T Previous { get; set; }
  }

  /// <summary>
  /// Implementation
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class TransactionMessage<T> : BaseMessage, ITransactionMessage<T>
  {
    /// <summary>
    /// Event type
    /// </summary>
    public virtual ActionEnum Action { get; set; }

    /// <summary>
    /// Current or next value to be set
    /// </summary>
    public virtual T Next { get; set; }

    /// <summary>
    /// Previous value
    /// </summary>
    public virtual T Previous { get; set; }
  }
}
