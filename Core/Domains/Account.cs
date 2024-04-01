using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    /// Leverage
    /// </summary>
    double? Leverage { get; set; }

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
    string Name { get; set; }

    /// <summary>
    /// Currency
    /// </summary>
    string Currency { get; set; }

    /// <summary>
    /// History of orders
    /// </summary>
    ObservableCollection<OrderModel?> Orders { get; set; }

    /// <summary>
    /// Completed trades
    /// </summary>
    ObservableCollection<PositionModel?> Positions { get; set; }

    /// <summary>
    /// Active orders
    /// </summary>
    IDictionary<string, OrderModel?> ActiveOrders { get; set; }

    /// <summary>
    /// Active positions
    /// </summary>
    IDictionary<string, PositionModel?> ActivePositions { get; set; }

    /// <summary>
    /// List of instruments
    /// </summary>
    IDictionary<string, Instrument> Instruments { get; set; }
  }

  /// <summary>
  /// Implementation
  /// </summary>
  public class Account : IAccount
  {
    /// <summary>
    /// Leverage
    /// </summary>
    public double? Leverage { get; set; }

    /// <summary>
    /// Balance
    /// </summary>
    public double? Balance { get; set; }

    /// <summary>
    /// State of the account in the beginning
    /// </summary>
    public double? InitialBalance { get; set; }

    /// <summary>
    /// Name
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Currency
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// History of completed orders
    /// </summary>
    public ObservableCollection<OrderModel?> Orders { get; set; }

    /// <summary>
    /// History of completed deals, closed positions
    /// </summary>
    public ObservableCollection<PositionModel?> Positions { get; set; }

    /// <summary>
    /// Active orders
    /// </summary>
    public IDictionary<string, OrderModel?> ActiveOrders { get; set; }

    /// <summary>
    /// Active positions
    /// </summary>
    public IDictionary<string, PositionModel?> ActivePositions { get; set; }

    /// <summary>
    /// List of instruments
    /// </summary>
    public IDictionary<string, Instrument> Instruments { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public Account()
    {
      Balance = 0.0;
      Leverage = 1.0;
      InitialBalance = 0.0;
      Currency = nameof(CurrencyEnum.USD);

      Orders = new ObservableCollection<OrderModel?>();
      Positions = new ObservableCollection<PositionModel?>();
      ActiveOrders = new Dictionary<string, OrderModel?>();
      ActivePositions = new Dictionary<string, PositionModel?>();
      Instruments = new Dictionary<string, Instrument>();
    }
  }
}
