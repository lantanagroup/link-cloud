using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application;

namespace DataAcquisition.AcquisitionWorker.Listeners;

public class ReadyToAcquireListener : BaseListener<ReadyToAcquire, Null, ReadyTo>
{
}
