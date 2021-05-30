using Core.EnumSpace;
using Core.ModelSpace;
using ExScore.ModelSpace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace Client.ControlSpace
{
  public partial class StatementsControl : UserControl
  {
    /// <summary>
    /// Statements
    /// </summary>
    protected IDictionary<string, IEnumerable<ScoreData>> Stats = new Dictionary<string, IEnumerable<ScoreData>>();

    /// <summary>
    /// Constructor
    /// </summary>
    public StatementsControl()
    {
      InitializeComponent();
      CreateStats();
    }

    /// <summary>
    /// Calculate metrics
    /// </summary>
    /// <returns></returns>
    protected void CreateStats()
    {
      var processors = InstanceManager<ResponseModel<IProcessorModel>>.Instance.Items;
      var accounts = processors.SelectMany(processor => processor.Gateways.Select(o => o.Account));
      var positions = accounts.SelectMany(account => account.Positions).OrderBy(o => o.Time).ToList();
      var balance = accounts.Sum(o => o.InitialBalance).Value;
      var values = new List<InputData>();

      for (var i = 0; i < positions.Count; i++)
      {
        var current = positions.ElementAtOrDefault(i);
        var previous = positions.ElementAtOrDefault(i - 1);
        var currentPoint = current?.GainLoss ?? 0.0;
        var previousPoint = values.ElementAtOrDefault(i - 1)?.Value ?? balance;

        values.Add(new InputData
        {
          Time = current.Time.Value,
          Value = previousPoint + currentPoint,
          Min = previousPoint + current.GainLossMin.Value,
          Max = previousPoint + current.GainLossMax.Value,
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

      ContentControl.ItemsSource = Stats = new Metrics { Values = values }.Calculate();
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
