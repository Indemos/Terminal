using System.Collections;
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
    ObservableCollection<OrderModel> Orders { get; set; }

    /// <summary>
    /// Completed trades
    /// </summary>
    ObservableCollection<PositionModel> Positions { get; set; }

    /// <summary>
    /// Active orders
    /// </summary>
    IDictionary<string, OrderModel> ActiveOrders { get; set; }

    /// <summary>
    /// Active positions
    /// </summary>
    IDictionary<string, PositionModel> ActivePositions { get; set; }

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
    public virtual ObservableCollection<OrderModel> Orders { get; set; }

    /// <summary>
    /// History of completed deals, closed positions
    /// </summary>
    public virtual ObservableCollection<PositionModel> Positions { get; set; }

    /// <summary>
    /// Active orders
    /// </summary>
    public virtual IDictionary<string, OrderModel> ActiveOrders { get; set; }

    /// <summary>
    /// Active positions
    /// </summary>
    public virtual IDictionary<string, PositionModel> ActivePositions { get; set; }

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
      Instruments = new Dictionary<string, InstrumentModel>();
      ActiveOrders = new Dictionary<string, OrderModel>();
      ActivePositions = new Dictionary<string, PositionModel>();
    }
  }
}
