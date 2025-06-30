using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Terminal.Core.Domains;
using Terminal.Core.Enums;
using Terminal.Core.Extensions;

namespace Terminal.Core.Models
{
  public class OrderModel : ICloneable
  {
    /// <summary>
    /// Order ID
    /// </summary>
    public virtual string Id { get; set; }

    /// <summary>
    /// Instrument name
    /// </summary>
    public virtual string Name { get; set; }

    /// <summary>
    /// Group
    /// </summary>
    public virtual string Descriptor { get; set; }

    /// <summary>
    /// Contract size
    /// </summary>
    public virtual double? Amount { get; set; }

    /// <summary>
    /// Size of partially filled contract
    /// </summary>
    public virtual double? OpenAmount { get; set; }

    /// <summary>
    /// Current PnL
    /// </summary>
    public virtual double? Gain { get; set; }

    /// <summary>
    /// Min possible PnL in account's currency
    /// </summary>
    public virtual double? GainMin { get; set; }

    /// <summary>
    /// Max possible PnL in account's currency
    /// </summary>
    public virtual double? GainMax { get; set; }

    /// <summary>
    /// Price the makes order active, e.g. limit price for stop limit order
    /// </summary>
    public virtual double? ActivationPrice { get; set; }

    /// <summary>
    /// Desired price for the order to fill, e.g. stop price for stop order and limit price for limit order
    /// </summary>
    public virtual double? OpenPrice { get; set; }

    /// <summary>
    /// Open price for the order
    /// </summary>
    public virtual double? Price { get; set; }

    /// <summary>
    /// Time stamp
    /// </summary>
    public virtual DateTime? Time { get; set; }

    /// <summary>
    /// Type
    /// </summary>
    public virtual OrderTypeEnum? Type { get; set; }

    /// <summary>
    /// Side
    /// </summary>
    public virtual OrderSideEnum? Side { get; set; }

    /// <summary>
    /// Status of the order, e.g. Pending
    /// </summary>
    public virtual OrderStatusEnum? Status { get; set; }

    /// <summary>
    /// Time in force
    /// </summary>
    public virtual OrderTimeSpanEnum? TimeSpan { get; set; }

    /// <summary>
    /// Custom order type
    /// </summary>
    public virtual InstructionEnum? Instruction { get; set; }

    /// <summary>
    /// Account
    /// </summary>
    public virtual IAccount Account { get; set; }

    /// <summary>
    /// List of related orders in the hierarchy
    /// </summary>
    public virtual IList<OrderModel> Orders { get; set; }

    /// <summary>
    /// Order events
    /// </summary>
    [JsonIgnore]
    public virtual Action<MessageModel<OrderModel>> OrderStream { get; set; }

    /// <summary>
    /// Summary
    /// </summary>
    public virtual InstrumentModel Instrument => Account.States.Get(Name).Instrument;

    /// <summary>
    /// Constructor
    /// </summary>
    public OrderModel()
    {
      Orders = [];
      OrderStream = o => { };
      Id = $"{Guid.NewGuid()}";
      Descriptor = $"{Guid.NewGuid()}";
    }

    /// <summary>
    /// Position direction
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    public double? GetDirection()
    {
      switch (Side)
      {
        case OrderSideEnum.Long: return 1;
        case OrderSideEnum.Short: return -1;
      }

      return null;
    }

    /// <summary>
    /// Position direction
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    public double? GetAmount()
    {
      var volume = OpenAmount ?? 0;
      var sideVolume = Orders.Sum(o => o.OpenAmount ?? 0);

      return volume + sideVolume;
    }

    /// <summary>
    /// Estimate open price for one of the instruments in the order
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    public virtual double? GetOpenPrice()
    {
      var point = Instrument.Point;

      if (point is not null)
      {
        switch (Side)
        {
          case OrderSideEnum.Long: return point.Ask;
          case OrderSideEnum.Short: return point.Bid;
        }
      }

      return null;
    }

    /// <summary>
    /// Estimate close price for one of the instruments in the order
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    public virtual double? GetClosePrice()
    {
      var point = Instrument.Point;

      if (point is not null)
      {
        switch (Side)
        {
          case OrderSideEnum.Long: return point.Bid;
          case OrderSideEnum.Short: return point.Ask;
        }
      }

      return null;
    }

    /// <summary>
    /// Estimated PnL in points for one side of the order
    /// </summary>
    /// <param name="price"></param>
    /// <returns></returns>
    public double? GetEstimate(double? price = null)
    {
      return ((price ?? GetClosePrice()) - OpenPrice) * GetDirection();
    }

    /// <summary>
    /// Estimated PnL in account's currency for one side of the order
    /// </summary>
    /// <param name="price"></param>
    /// <returns></returns>
    public double? GetValueEstimate(double? price = null)
    {
      var asset = Instrument;
      var step = asset.StepValue / asset.StepSize;
      var estimate = OpenAmount * GetEstimate(price) * step * asset.Leverage - asset.Commission;

      Gain = estimate ?? Gain ?? 0;
      GainMin = Math.Min(GainMin ?? 0, Gain.Value);
      GainMax = Math.Max(GainMax ?? 0, Gain.Value);

      return estimate;
    }

    /// <summary>
    /// Clone
    /// </summary>
    public virtual object Clone() => MemberwiseClone();
  }
}
