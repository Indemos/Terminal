using ExScore.ModelSpace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Core.EnumSpace;
using Terminal.Core.ModelSpace;

namespace Terminal.Client.Components
{
  public partial class StatementsComponent
  {
    /// <summary>
    /// Common performance statistics
    /// </summary>
    protected IDictionary<string, IList<ScoreData>> Stats = new Dictionary<string, IList<ScoreData>>();

    /// <summary>
    /// Update UI
    /// </summary>
    /// <param name="accounts"></param>
    public virtual Task UpdateItems(IList<IAccountModel> accounts)
    {
      var values = new List<InputData>();
      var positions = accounts.SelectMany(account => account.Positions).OrderBy(o => o.Time).ToList();
      var balance = accounts.Sum(o => o.InitialBalance).Value;

      if (positions.Any())
      {
        values.Add(new InputData
        {
          Time = positions.First().Time.Value,
          Value = balance,
          Min = balance,
          Max = balance
        });
      }

      for (var i = 0; i < positions.Count; i++)
      {
        var position = positions[i];
        var previousInput = values[i].Value;

        values.Add(new InputData
        {
          Time = position.Time.Value,
          Value = previousInput + position.GainLoss.Value,
          Min = previousInput + position.GainLossMin.Value,
          Max = previousInput + position.GainLossMax.Value,
          Commission = position.Instrument.Commission.Value * 2,
          Direction = GetDirection(position)
        });
      }

      Stats = new Score { Values = values }.Calculate();

      return InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Order side to bonary direction
    /// </summary>
    /// <param name="position"></param>
    /// <returns></returns>
    protected virtual int GetDirection(ITransactionPositionModel position)
    {
      switch (position.Side)
      {
        case OrderSideEnum.Buy: return 1;
        case OrderSideEnum.Sell: return -1;
      }

      return 0;
    }

    /// <summary>
    /// Format double
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    protected virtual string ShowDouble(double? input)
    {
      var sign = " ";

      if (input < 0)
      {
        sign = "-";
      }

      return sign + string.Format("{0:0.00}", Math.Abs(input.Value));
    }
  }
}
