using Core.Models;
using System.Collections.Generic;

namespace Core.Groups
{
  public interface IGroup
  {
    /// <summary>
    /// List of price groups
    /// </summary>
    List<Price> Items { get; }

    /// <summary>
    /// Combine price into bar
    /// </summary>
    /// <param name="price"></param>
    Price Update(Price price);
  }

  public abstract class Group : IGroup
  {
    /// <summary>
    /// List of prices groups
    /// </summary>
    public virtual List<Price> Items { get; protected set; } = [];

    /// <summary>
    /// Combine
    /// </summary>
    /// <param name="price"></param>
    public abstract Price Update(Price price);
  }
}
