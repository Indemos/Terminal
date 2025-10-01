namespace Core.Common.Models
{
  public record BalanceModel
  {
    /// <summary>
    /// Current PnL
    /// </summary>
    public double? Current { get; init; }

    /// <summary>
    /// Min possible PnL in account's currency
    /// </summary>
    public double? Min { get; init; }

    /// <summary>
    /// Max possible PnL in account's currency
    /// </summary>
    public double? Max { get; init; }
  }
}
