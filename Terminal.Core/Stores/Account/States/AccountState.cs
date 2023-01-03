using Distribution.AttributeSpace;
using System.Threading.Tasks;
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
  }
}
