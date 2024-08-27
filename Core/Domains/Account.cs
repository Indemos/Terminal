using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Terminal.Core.Enums;
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
    /// History of orders
    /// </summary>
    ConcurrentQueue<OrderModel> Orders { get; set; }

    /// <summary>
    /// Completed trades
    /// </summary>
    ConcurrentQueue<PositionModel> Positions { get; set; }

    /// <summary>
    /// Active orders
    /// </summary>
    ConcurrentQueue<OrderModel> ActiveOrders { get; set; }

    /// <summary>
    /// Active positions
    /// </summary>
    ConcurrentQueue<PositionModel> ActivePositions { get; set; }

    /// <summary>
    /// List of instruments
    /// </summary>
    IDictionary<string, InstrumentModel> Instruments { get; set; }
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
    /// History of completed orders
    /// </summary>
    public virtual ConcurrentQueue<OrderModel> Orders { get; set; }

    /// <summary>
    /// History of completed deals, closed positions
    /// </summary>
    public virtual ConcurrentQueue<PositionModel> Positions { get; set; }

    /// <summary>
    /// Active orders
    /// </summary>
    public virtual ConcurrentQueue<OrderModel> ActiveOrders { get; set; }

    /// <summary>
    /// Active positions
    /// </summary>
    public virtual ConcurrentQueue<PositionModel> ActivePositions { get; set; }

    /// <summary>
    /// List of instruments
    /// </summary>
    public virtual IDictionary<string, InstrumentModel> Instruments { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public Account()
    {
      Balance = 0.0;
      InitialBalance = 0.0;

      Orders = [];
      Positions = [];
      ActiveOrders = [];
      ActivePositions = [];
      Instruments = new Dictionary<string, InstrumentModel>();
    }
  }
}
