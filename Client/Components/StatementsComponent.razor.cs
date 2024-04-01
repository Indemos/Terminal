using Estimator.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Models;

namespace Client.Components
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
    public Task UpdateItems(IList<IAccount> accounts)
    {
      var values = new List<InputData>();
      var positions = accounts.SelectMany(account => account.Positions).OrderBy(o => o?.Order?.Transaction?.Time).ToList();
      var balance = accounts.Sum(o => o.InitialBalance).Value;

      if (positions.Any())
      {
        values.Add(new InputData
        {
          Time = positions.First().Value.Order.Value.Transaction.Value.Time.Value,
          Value = 0,
          Min = 0,
          Max = 0
        });
      }

      for (var i = 0; i < positions.Count; i++)
      {
        var position = positions[i];

        values.Add(new InputData
        {
          Time = position.Value.Order.Value.Transaction.Value.Time.Value,
          Value = position.Value.GainLoss.Value,
          Min = position.Value.GainLossMin.Value,
          Max = position.Value.GainLossMax.Value,
          Commission = position.Value.Order.Value.Transaction.Value.Instrument.Commission.Value * 2,
          Direction = GetDirection(position)
        });
      }

      Stats = new Score { Items = values, Balance = balance }.Calculate();

      return InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Order side to bonary direction
    /// </summary>
    /// <param name="position"></param>
    /// <returns></returns>
    protected int GetDirection(PositionModel? position)
    {
      switch (position?.Order?.Side)
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
      return (input < 0 ? "-" : "") + string.Format("{0:0.00}", Math.Abs(input.Value));
    }
  }
}
