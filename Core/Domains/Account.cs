using System.Collections.Concurrent;
using System.Collections.Generic;
using Terminal.Core.Collections;
using Terminal.Core.Models;

namespace Terminal.Core.Domains
{
  /// <summary>
  /// Generic account interface
  /// </summary>
  public interface IAccount
  {
    /// <summary>
    /// Balance
    /// </summary>
    double? Balance { get; set; }

    /// <summary>
    /// State of the account in the beginning
    /// </summary>
    double? InitialBalance { get; set; }

    /// <summary>
    /// Name
    /// </summary>
    string Descriptor { get; set; }

    /// <summary>
    /// Completed trades
    /// </summary>
    IList<OrderModel> Deals { get; set; }

    /// <summary>
    /// Active orders
    /// </summary>
    ConcurrentDictionary<string, OrderModel> Orders { get; set; }

    /// <summary>
    /// Active positions
    /// </summary>
    ConcurrentDictionary<string, OrderModel> Positions { get; set; }

    /// <summary>
    /// Snapshots
    /// </summary>
    Map<string, SummaryModel> States { get; set; }
  }

  /// <summary>
  /// Implementation
  /// </summary>
  public class Account : IAccount
  {
    /// <summary>
    /// Balance
    /// </summary>
    public virtual double? Balance { get; set; }

    /// <summary>
    /// State of the account in the beginning
    /// </summary>
    public virtual double? InitialBalance { get; set; }

    /// <summary>
    /// Name
    /// </summary>
    public virtual string Descriptor { get; set; }

    /// <summary>
    /// History of completed deals, closed positions
    /// </summary>
    public virtual IList<OrderModel> Deals { get; set; }

    /// <summary>
    /// Active orders
    /// </summary>
    public virtual ConcurrentDictionary<string, OrderModel> Orders { get; set; }

    /// <summary>
    /// Active positions
    /// </summary>
    public virtual ConcurrentDictionary<string, OrderModel> Positions { get; set; }

    /// <summary>
    /// Market snapshot
    /// </summary>
    public virtual Map<string, SummaryModel> States { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public Account()
    {
      Balance = 0.0;
      InitialBalance = 0.0;

      Deals = [];
      Orders = new();
      Positions = new();
      States = new();
    }
  }
}
