using Confluent.Kafka;
using LantanaGroup.Link.DemoApiGateway.Application.models.testing;

namespace LantanaGroup.Link.DemoApiGateway.Application.models.interfaces
{
    public interface IKafkaProducerFactory
    {
        public IProducer<ReportScheduledKey, ReportScheduledMessage> CreateReportScheduledProducer(string clientId);

        public IProducer<string, PatientEventMessage> CreatePatientEventProducer(string clientId);

        public IProducer<string, DataAcquisitionRequestedMessage> CreateDataAcquisitionRequestedProducer(string clientId);
    }
}
