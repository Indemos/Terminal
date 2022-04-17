using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;

namespace Terminal.Core.ModelSpace
{
  public interface IExpandoModel : IDynamicMetaObjectProvider, ICloneable
  {
  }

  /// <summary>
  /// Expando class that allows to extend other models in runtime
  /// </summary>
  public class ExpandoModel : DynamicObject, IExpandoModel
  {
    /// <summary>
    /// Internal dictionary to keep dynamic properties
    /// </summary>
    protected IDictionary<string, dynamic> _items = null;

    /// <summary>
    /// Constructor
    /// </summary>
    public ExpandoModel()
    {
      _items = new Dictionary<string, dynamic>();
    }

    /// <summary>
    /// Redirect setter for dynamic properties to internal dictionary
    /// </summary>
    /// <param name="binder"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public override bool TrySetMember(SetMemberBinder binder, object value)
    {
      _items[binder.Name] = value;
      return true;
    }

    /// <summary>
    /// Redirect getter for dynamic properties to internal dictionary
    /// </summary>
    /// <param name="binder"></param>
    /// <param name="o"></param>
    /// <returns></returns>
    public override bool TryGetMember(GetMemberBinder binder, out object o) => _items.TryGetValue(binder.Name, out o);

    /// <summary>
    /// Get all properties for serialization
    /// </summary>
    /// <returns></returns>
    public override IEnumerable<string> GetDynamicMemberNames() => GetType()
      .GetProperties()
      .Select(o => o.Name)
      .Concat(_items.Keys);

    /// <summary>
    /// Implement indexer for internal dictionary
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public virtual object this[object index]
    {
      get => _items.TryGetValue($"{ index }", out object o) ? o : null;
      set => _items[$"{ index }"] = value;
    }

    /// <summary>
    /// Clone
    /// </summary>
    /// <returns></returns>
    public virtual object Clone() => MemberwiseClone();
  }
}
