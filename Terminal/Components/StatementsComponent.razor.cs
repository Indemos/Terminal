using Distribution.Services;
using Estimator.Models;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Services;

namespace Terminal.Components
{
  public partial class StatementsComponent
  {
    [Parameter] public virtual string Name { get; set; }

    [Parameter] public virtual IDictionary<string, IGateway> Adapters { get; set; }

    /// <summary>
    /// Subscription state
    /// </summary>
    protected virtual SubscriptionService Subscription { get => InstanceService<SubscriptionService>.Instance; }

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
        Subscription.Update += state =>
        {
          switch (true)
          {
            case true when state.Previous is SubscriptionEnum.Progress && state.Next is SubscriptionEnum.None:
              Clear();
              break;

            case true when state.Previous is SubscriptionEnum.Progress && state.Next is SubscriptionEnum.Pause:
              UpdateItems(Adapters.Values.Select(o => o.Account));
              break;
          }
        };
      }
    }

    /// <summary>
    /// Update UI
    /// </summary>
    /// <param name="accounts"></param>
    public virtual Task UpdateItems(IEnumerable<IAccount> accounts)
    {
      var values = new List<InputData>();
      var balance = accounts.Sum(o => o.InitialBalance).Value;
      var orders = accounts
        .SelectMany(account => account.Deals.SelectMany(o => o.Orders.Append(o)))
        .OrderBy(o => o.Transaction.Time)
        .ToList();

      if (orders.Count > 0)
      {
        values.Add(new InputData
        {
          Time = orders.First().Transaction.Time.Value,
          Value = 0,
          Min = 0,
          Max = 0
        });
      }

      for (var i = 0; i < orders.Count; i++)
      {
        var o = orders[i];

        values.Add(new InputData
        {
          Min = o.Min.Value,
          Max = o.Max.Value,
          Value = o.Gain.Value,
          Time = o.Transaction.Time.Value,
          Direction = o.GetSide().Value,
          Commission = o.Transaction.Instrument.Commission.Value * 2
        });
      }

      Stats = new Score { Items = values, Balance = balance }.Calculate();

      return InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Clear records
    /// </summary>
    public virtual void Clear() => UpdateItems([]);

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
