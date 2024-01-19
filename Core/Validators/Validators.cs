using FluentValidation;
using System.Collections.Generic;
using Terminal.Core.Services;

namespace Terminal.Core.Validators
{
  /// <summary>
  /// Validation cache
  /// </summary>
  public class Validators
  {
    public static IDictionary<string, IValidator> Instances { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public Validators()
    {
      Instances = new Dictionary<string, IValidator>
      {
        [nameof(PointValidator)] = InstanceService<PointValidator>.Instance,
        [nameof(PointVolumeValidator)] = InstanceService<PointVolumeValidator>.Instance,
        [nameof(PointCollectionValidator)] = InstanceService<PointCollectionValidator>.Instance,
        [nameof(BarValidator)] = InstanceService<BarValidator>.Instance,
        [nameof(AccountValidator)] = InstanceService<AccountValidator>.Instance,
        [nameof(AccountCollectionValidator)] = InstanceService<AccountCollectionValidator>.Instance,
        [nameof(InstrumentValidator)] = InstanceService<InstrumentValidator>.Instance,
        [nameof(InstrumentCollectionValidator)] = InstanceService<InstrumentCollectionValidator>.Instance,
        [nameof(OptionValidator)] = InstanceService<OptionValidator>.Instance,
        [nameof(FutureValidator)] = InstanceService<FutureValidator>.Instance,
        [nameof(TransactionValidator)] = InstanceService<TransactionValidator>.Instance,
        [nameof(OrderValidator)] = InstanceService<OrderValidator>.Instance,
        [nameof(OrderPriceValidator)] = InstanceService<OrderPriceValidator>.Instance,
        [nameof(PositionValidator)] = InstanceService<PositionValidator>.Instance,
        [nameof(PositionGainLossValidator)] = InstanceService<PositionGainLossValidator>.Instance
      };
    }
  }
}
