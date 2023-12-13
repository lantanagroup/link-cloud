using Confluent.Kafka;
using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.QueryDispatch.Application.PatientDispatch.Commands
{
    public class DeletePatientDispatchCommand : IDeletePatientDispatchCommand
    {
        private readonly ILogger<DeletePatientDispatchCommand> _logger;
        private readonly IKafkaProducerFactory<string, AuditEventMessage> _kafkaProducerFactory;
        private readonly IPatientDispatchRepository _dataStore;

        public DeletePatientDispatchCommand(ILogger<DeletePatientDispatchCommand> logger, IKafkaProducerFactory<string, AuditEventMessage> kafkaProducerFactory, IPatientDispatchRepository dataStore) 
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaProducerFactory = kafkaProducerFactory ?? throw new ArgumentNullException(nameof(kafkaProducerFactory));
            _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
        }

        public async Task<bool> Execute(string facilityId, string patientId)
        {
            try
            {
                bool result = await _dataStore.Delete(facilityId, patientId);

                _logger.LogInformation($"Deleted Patient Dispatch record for patient id {patientId} in facility {facilityId}");

                using (var producer = _kafkaProducerFactory.CreateAuditEventProducer())
                {
                    var headers = new Headers
                    {
                        { "X-Correlation-Id", Guid.NewGuid().ToByteArray() }
                    };

                    var auditMessage = new AuditEventMessage
                    {
                        FacilityId = facilityId,
                        ServiceName = QueryDispatchConstants.ServiceName,
                        Action = AuditEventType.Delete,
                        EventDate = DateTime.UtcNow,
                        Resource = typeof(PatientDispatchEntity).Name,
                        Notes = $"Deleted Patient Dispatch record for patient id {patientId} in facility {facilityId}"
                    };

                    producer.Produce(nameof(KafkaTopic.AuditableEventOccurred), new Message<string, AuditEventMessage>
                    {
                        Value = auditMessage,
                        Headers = headers
                    });

                    producer.Flush();
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Patient dispatch delete exception for patientId {patientId} in facility {facilityId}.", ex);
                throw new ApplicationException($"Failed to delete patient dispatch record for patient id {patientId} in facility {facilityId}.");
            }
        }
    }
}
