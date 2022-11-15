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
    public Task UpdateItems(IList<IAccountModel> accounts)
    {
      var positions = accounts.SelectMany(account => account.Positions).OrderBy(o => o.Time).ToList();
      var balance = accounts.Sum(o => o.InitialBalance).Value;
      var values = new List<InputData>();

      for (var i = 0; i < positions.Count; i++)
      {
        var current = positions.ElementAtOrDefault(i);
        var previous = positions.ElementAtOrDefault(i - 1);
        var currentPoint = current?.GainLoss ?? 0.0;
        var previousPoint = values.ElementAtOrDefault(i - 1).Value;
        var setup = i == 0 ? balance : 0;

        values.Add(new InputData
        {
          Time = current.Time.Value,
          Value = previousPoint + currentPoint + setup,
          Min = previousPoint + current.GainLossMin.Value + setup,
          Max = previousPoint + current.GainLossMax.Value + setup,
          Commission = current.Instrument.Commission.Value,
          Direction = GetDirection(current)
        });
      }

      if (values.Any())
      {
        values.Insert(0, new InputData
        {
          Min = balance,
          Max = balance,
          Value = balance,
          Time = values.First().Time,
          Commission = 0.0,
          Direction = 0
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
    protected int GetDirection(ITransactionPositionModel position)
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
    protected string ShowDouble(double? input)
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
