using Distribution.AttributeSpace;
using System.Threading.Tasks;
using Terminal.Core.EnumSpace;
using Terminal.Core.MessageSpace;
using Terminal.Core.ModelSpace;

namespace Terminal.Core.StoreSpace
{
  public class AccountState
  {
    /// <summary>
    /// Account 
    /// </summary>
    protected IAccountModel _account = null;

    public AccountState()
    {
      _account = new AccountModel();
    }

    [Processor]
    public virtual Task<AccountSelector> CreateAccount(CreateAccountAction message)
    {
      return Task.FromResult(new AccountSelector
      {
        Account = _account = message.Account.Clone() as IAccountModel
      });
    }

    protected virtual IPointModel UpdatePoints(IPointModel point)
    {
      var instrument = _account.Instruments[point.Name];

      point.Account = _account;
      point.Instrument = instrument;
      point.TimeFrame = instrument.TimeFrame;

      instrument.Points.Add(point);
      instrument.PointGroups.Add(point, instrument.TimeFrame);

      return point;
    }
  }
}
