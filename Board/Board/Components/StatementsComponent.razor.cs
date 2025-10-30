using Core.Conventions;
using Core.Enums;
using Core.Services;
using Estimator.Models;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Board.Components
{
  public partial class StatementsComponent
  {
    [Inject] public virtual StateService Observer { get; set; }

    [Parameter] public virtual string Name { get; set; }

    [Parameter] public virtual IDictionary<string, IGateway> Adapters { get; set; }

    /// <summary>
    /// Common performance statistics
    /// </summary>
    protected IDictionary<string, IList<ScoreData>> Stats = new Dictionary<string, IList<ScoreData>>();

    /// <summary>
    /// Setup views
    /// </summary>
    /// <param name="setup"></param>
    protected override async Task OnAfterRenderAsync(bool setup)
    {
      await base.OnAfterRenderAsync(setup);

      if (setup)
      {
        Observer.Update += state =>
        {
          switch (true)
          {
            case true when state.Previous is SubscriptionEnum.Progress && state.Next is SubscriptionEnum.None:
              Clear();
              break;

            case true when state.Previous is SubscriptionEnum.Progress && state.Next is SubscriptionEnum.Pause:
              UpdateItems([.. Adapters.Values]);
              break;
          }

          return Task.CompletedTask;
        };
      }
    }

    /// <summary>
    /// Update UI
    /// </summary>
    /// <param name="adapters"></param>
    public virtual async void UpdateItems(params IGateway[] adapters)
    {
      if (Observer.State.Next is SubscriptionEnum.None)
      {
        return;
      }

      var values = new List<InputData>();
      var actionResponses = await Task.WhenAll(adapters.Select(o => o.GetTransactions(default)));
      var balance = adapters.Sum(o => o.Account.Balance ?? 0);
      var actions = actionResponses
        .SelectMany(o => o)
        .OrderBy(o => o.Operation.Time)
        .ToList();

      if (actions.Count > 0)
      {
        values.Add(new InputData
        {
          Time = new DateTime(actions.First().Operation.Time.Value),
          Value = 0,
          Min = 0,
          Max = 0
        });
      }

      foreach (var o in actions)
      {
        values.Add(new InputData
        {
          Min = o.Balance.Min ?? 0,
          Max = o.Balance.Max ?? 0,
          Value = o.Balance.Current ?? 0,
          Time = new DateTime(o.Operation.Time.Value),
          Direction = o.Side is OrderSideEnum.Long ? 1 : -1,
          Commission = o.Operation.Instrument.Commission.Value * 2
        });
      }

      Stats = new Score { Items = values, Balance = balance }.Calculate();

      await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Clear records
    /// </summary>
    public virtual void Clear() => Stats = new Dictionary<string, IList<ScoreData>>();

    /// <summary>
    /// Format double
    /// </summary>
    /// <param name="input"></param>
    protected virtual string ShowDouble(double? input)
    {
      return (input < 0 ? "-" : "") + string.Format("{0:0.00}", Math.Abs(input.Value));
    }
  }
}
