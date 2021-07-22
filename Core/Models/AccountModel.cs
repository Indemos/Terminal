using Core.CollectionSpace;
using Core.EnumSpace;
using FluentValidation;

namespace Core.ModelSpace
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
    CurrencyEnum Currency { get; set; }

    /// <summary>
    /// Reference to the gateway
    /// </summary>
    IGatewayModel Gateway { get; set; }

    /// <summary>
    /// History of orders
    /// </summary>
    IIndexCollection<ITransactionOrderModel> Orders { get; set; }

    /// <summary>
    /// Active orders
    /// </summary>
    IIndexCollection<ITransactionOrderModel> ActiveOrders { get; set; }

    /// <summary>
    /// Completed trades
    /// </summary>
    IIndexCollection<ITransactionPositionModel> Positions { get; set; }

    /// <summary>
    /// Active positions
    /// </summary>
    IIndexCollection<ITransactionPositionModel> ActivePositions { get; set; }

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
    public virtual CurrencyEnum Currency { get; set; }

    /// <summary>
    /// Reference to the gateway
    /// </summary>
    public virtual IGatewayModel Gateway { get; set; }

    /// <summary>
    /// History of completed orders
    /// </summary>
    public virtual IIndexCollection<ITransactionOrderModel> Orders { get; set; }

    /// <summary>
    /// Active orders
    /// </summary>
    public virtual IIndexCollection<ITransactionOrderModel> ActiveOrders { get; set; }

    /// <summary>
    /// History of completed deals, closed positions
    /// </summary>
    public virtual IIndexCollection<ITransactionPositionModel> Positions { get; set; }

    /// <summary>
    /// Active positions
    /// </summary>
    public virtual IIndexCollection<ITransactionPositionModel> ActivePositions { get; set; }

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
      Currency = CurrencyEnum.USD;

      Orders = new IndexCollection<ITransactionOrderModel>();
      ActiveOrders = new IndexCollection<ITransactionOrderModel>();
      Positions = new IndexCollection<ITransactionPositionModel>();
      ActivePositions = new IndexCollection<ITransactionPositionModel>();
      Instruments = new NameCollection<string, IInstrumentModel>();
    }
  }

  /// <summary>
  /// Validation rules
  /// </summary>
  public class AccountValidation : AbstractValidator<IAccountModel>
  {
    public AccountValidation()
    {
      RuleFor(o => o.Leverage).NotNull().NotEqual(0).WithMessage("No leverage");
      RuleFor(o => o.Balance).NotNull().WithMessage("No balance");
      RuleFor(o => o.InitialBalance).NotNull().WithMessage("No initial balance");
      RuleFor(o => o.Currency).NotNull().WithMessage("No currency");
      RuleFor(o => o.Instruments).NotNull().WithMessage("No instruments");
      RuleFor(o => o.Orders).NotNull().WithMessage("No orders");
      RuleFor(o => o.ActiveOrders).NotNull().WithMessage("No active orders");
      RuleFor(o => o.Positions).NotNull().WithMessage("No positions");
      RuleFor(o => o.ActivePositions).NotNull().WithMessage("No active positions");
    }
  }

  /// <summary>
  /// Validation rules
  /// </summary>
  public class AccountCollectionsValidation : AbstractValidator<IAccountModel>
  {
    public AccountCollectionsValidation()
    {
      Include(new AccountValidation());

      RuleFor(o => o.Instruments).NotNull().NotEmpty().WithMessage("No instruments");
      RuleFor(o => o.Orders).NotNull().NotEmpty().WithMessage("No orders");
      RuleFor(o => o.ActiveOrders).NotNull().NotEmpty().WithMessage("No active orders");
      RuleFor(o => o.Positions).NotNull().NotEmpty().WithMessage("No positions");
      RuleFor(o => o.ActivePositions).NotNull().NotEmpty().WithMessage("No active positions");
    }
  }
}
