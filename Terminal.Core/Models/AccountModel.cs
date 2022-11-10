using Terminal.Core.CollectionSpace;
using Terminal.Core.EnumSpace;

namespace Terminal.Core.ModelSpace
{
  /// <summary>
  /// Generic account interface
  /// </summary>
  public interface IAccountModel : IBaseModel
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
    /// Currency
    /// </summary>
    string Currency { get; set; }

    /// <summary>
    /// History of orders
    /// </summary>
    IIndexCollection<ITransactionOrderModel> Orders { get; set; }

    /// <summary>
    /// Completed trades
    /// </summary>
    IIndexCollection<ITransactionPositionModel> Positions { get; set; }

    /// <summary>
    /// Active orders
    /// </summary>
    INameCollection<string, ITransactionOrderModel> ActiveOrders { get; set; }

    /// <summary>
    /// Active positions
    /// </summary>
    INameCollection<string, ITransactionPositionModel> ActivePositions { get; set; }

    /// <summary>
    /// List of instruments
    /// </summary>
    INameCollection<string, IInstrumentModel> Instruments { get; set; }
  }

  /// <summary>
  /// Implementation
  /// </summary>
  public class AccountModel : BaseModel, IAccountModel
  {
    /// <summary>
    /// Leverage
    /// </summary>
    public virtual double? Leverage { get; set; }

    /// <summary>
    /// Balance
    /// </summary>
    public virtual double? Balance { get; set; }

    /// <summary>
    /// State of the account in the beginning
    /// </summary>
    public virtual double? InitialBalance { get; set; }

    /// <summary>
    /// Currency
    /// </summary>
    public virtual string Currency { get; set; }

    /// <summary>
    /// History of completed orders
    /// </summary>
    public virtual IIndexCollection<ITransactionOrderModel> Orders { get; set; }

    /// <summary>
    /// History of completed deals, closed positions
    /// </summary>
    public virtual IIndexCollection<ITransactionPositionModel> Positions { get; set; }

    /// <summary>
    /// Active orders
    /// </summary>
    public virtual INameCollection<string, ITransactionOrderModel> ActiveOrders { get; set; }

    /// <summary>
    /// Active positions
    /// </summary>
    public virtual INameCollection<string, ITransactionPositionModel> ActivePositions { get; set; }

    /// <summary>
    /// List of instruments
    /// </summary>
    public virtual INameCollection<string, IInstrumentModel> Instruments { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public AccountModel()
    {
      Balance = 0.0;
      Leverage = 1.0;
      InitialBalance = 0.0;
      Currency = nameof(CurrencyEnum.USD);

      Orders = new IndexCollection<ITransactionOrderModel>();
      Positions = new IndexCollection<ITransactionPositionModel>();
      ActiveOrders = new NameCollection<string, ITransactionOrderModel>();
      ActivePositions = new NameCollection<string, ITransactionPositionModel>();
      Instruments = new NameCollection<string, IInstrumentModel>();
    }
  }
}
