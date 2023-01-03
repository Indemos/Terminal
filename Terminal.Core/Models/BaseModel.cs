using System;

namespace Terminal.Core.ModelSpace
{
  public interface IBaseModel : IExpandoModel
  {
    /// <summary>
    /// Identity
    /// </summary>
    string Id { get; set; }

    /// <summary>
    /// Custom name for UI
    /// </summary>
    string Name { get; set; }

    /// <summary>
    /// Parameter that can be used to group a set of orders
    /// </summary>
    string Group { get; set; }

    /// <summary>
    /// Custom description for UI
    /// </summary>
    string Description { get; set; }
  }

  /// <summary>
  /// Expando class that allows to extend other models in runtime
  /// </summary>
  public class BaseModel : ExpandoModel, IBaseModel
  {
    /// <summary>
    /// Identity
    /// </summary>
    public virtual string Id { get; set; }

    /// <summary>
    /// Custom name for UI
    /// </summary>
    public virtual string Name { get; set; }

    /// <summary>
    /// Parameter that can be used to group a set of orders
    /// </summary>
    public virtual string Group { get; set; }

    /// <summary>
    /// Custom description for UI
    /// </summary>
    public virtual string Description { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public BaseModel()
    {
      Id = Guid.NewGuid().ToString("N");
    }
  }
}
