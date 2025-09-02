using Core.Client.Services;
using Core.Common.Enums;
using Core.Common.Implementations;
using Estimator.Models;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Client.Components
{
  public partial class StatementsComponent
  {
    [Inject] public virtual SubscriptionService Observer { get; set; }

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
        Observer.OnMessage += async state =>
        {
          switch (true)
          {
            case true when state.Previous is SubscriptionEnum.Progress && state.Next is SubscriptionEnum.None:
              Clear();
              break;

            case true when state.Previous is SubscriptionEnum.Progress && state.Next is SubscriptionEnum.Pause:
              await UpdateItems([.. Adapters.Values]);
              break;
          }
        };
      }
    }

    /// <summary>
    /// Update UI
    /// </summary>
    /// <param name="adapters"></param>
    public virtual async Task UpdateItems(params IGateway[] adapters)
    {
      var values = new List<InputData>();
      var balance = adapters.Sum(o => o.Account.Balance).Value;
      var queries = adapters.Select(o => o.GetTransactions());
      var responses = await Task.WhenAll(queries);
      var actions = responses
        .SelectMany(o => o.Data)
        .OrderBy(o => o.Operation.Time)
        .ToList();

      if (actions.Count > 0)
      {
        values.Add(new InputData
        {
          Time = actions.First().Operation.Time.Value,
          Value = 0,
          Min = 0,
          Max = 0
        });
      }

      foreach (var o in actions)
      {
        values.Add(new InputData
        {
          Min = o.Min.Value,
          Max = o.Max.Value,
          Value = o.Gain.Value,
          Time = o.Operation.Time.Value,
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
    public virtual async void Clear() => await UpdateItems([]);

    /// <summary>
    /// Format double
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    protected virtual string ShowDouble(double? input)
    {
      return (input < 0 ? "-" : "") + string.Format("{0:0.00}", Math.Abs(input.Value));
    }
  }
}
