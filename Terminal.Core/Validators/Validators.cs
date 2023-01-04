using System.Collections.Generic;
using Terminal.Core.ServiceSpace;

namespace Terminal.Core.ValidatorSpace
{
  /// <summary>
  /// Validation cache
  /// </summary>
  public class Validators
  {
    public static IDictionary<string, dynamic> Instances { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public Validators()
    {
      Instances = new Dictionary<string, dynamic>
      {
        [nameof(TimeValidator)] = InstanceService<TimeValidator>.Instance,
        [nameof(PointValidator)] = InstanceService<PointValidator>.Instance,
        [nameof(PointCollectionValidator)] = InstanceService<PointCollectionValidator>.Instance,
        [nameof(PointBarValidator)] = InstanceService<PointBarValidator>.Instance,
        [nameof(AccountValidator)] = InstanceService<AccountValidator>.Instance,
        [nameof(AccountCollectionValidator)] = InstanceService<AccountCollectionValidator>.Instance,
        [nameof(ConnectorValidator)] = InstanceService<ConnectorValidator>.Instance,
        [nameof(InstrumentValidator)] = InstanceService<InstrumentValidator>.Instance,
        [nameof(InstrumentCollectionValidator)] = InstanceService<InstrumentCollectionValidator>.Instance,
        [nameof(InstrumentOptionValidator)] = InstanceService<InstrumentOptionValidator>.Instance,
        [nameof(InstrumentFutureValidator)] = InstanceService<InstrumentFutureValidator>.Instance,
        [nameof(TransactionValidator)] = InstanceService<TransactionValidator>.Instance,
        [nameof(TransactionOrderValidator)] = InstanceService<TransactionOrderValidator>.Instance,
        [nameof(TransactionOrderPriceValidator)] = InstanceService<TransactionOrderPriceValidator>.Instance,
        [nameof(TransactionPositionValidator)] = InstanceService<TransactionPositionValidator>.Instance,
        [nameof(TransactionPositionGainLossValidator)] = InstanceService<TransactionPositionGainLossValidator>.Instance
      };
    }
  }
}
