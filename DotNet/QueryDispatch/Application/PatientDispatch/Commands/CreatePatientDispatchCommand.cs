using Quartz;
using Confluent.Kafka;
using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.QueryDispatch.Presentation.Services;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using QueryDispatch.Application.Settings;

namespace LantanaGroup.Link.QueryDispatch.Application.PatientDispatch.Commands
{
    public class CreatePatientDispatchCommand : ICreatePatientDispatchCommand
    {
        private readonly ILogger<CreatePatientDispatchCommand> _logger;
        private readonly IKafkaProducerFactory<string, AuditEventMessage> _kafkaProducerFactory;
        private readonly IPatientDispatchRepository _datastore;
        private readonly ISchedulerFactory _schedulerFactory;
        
        public CreatePatientDispatchCommand(ILogger<CreatePatientDispatchCommand> logger, IKafkaProducerFactory<string, AuditEventMessage> kafkaProducerFactory, IPatientDispatchRepository datastore, ISchedulerFactory schedulerFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaProducerFactory = kafkaProducerFactory ?? throw new ArgumentNullException(nameof(kafkaProducerFactory));
            _datastore = datastore ?? throw new ArgumentNullException(nameof(datastore));
            _schedulerFactory = schedulerFactory ?? throw new ArgumentNullException(nameof(schedulerFactory));
        }

        public async Task<string> Execute(PatientDispatchEntity patientDispatch, QueryDispatchConfigurationEntity queryDispatchConfiguration)
        {
            try
            {
                _datastore.Add(patientDispatch);

                _logger.LogInformation($"Created patient dispatch for patient id {patientDispatch.PatientId} in facility {patientDispatch.FacilityId}");

                await ScheduleService.CreateJobAndTrigger(patientDispatch, await _schedulerFactory.GetScheduler());


                using (var producer = _kafkaProducerFactory.CreateAuditEventProducer())
                {
                    var headers = new Headers
                    {
                        { "X-Correlation-Id", Guid.NewGuid().ToByteArray() }
                    };

                    var auditMessage = new AuditEventMessage
                    {
                        FacilityId = patientDispatch.FacilityId,
                        ServiceName = QueryDispatchConstants.ServiceName,
                        Action = AuditEventType.Create,
                        EventDate = DateTime.UtcNow,
                        Resource = typeof(PatientDispatchEntity).Name,
                        Notes = $"Created patient dispatch for patient id {patientDispatch.PatientId} in facility {patientDispatch.FacilityId}"
                    };

                    producer.Produce(nameof(KafkaTopic.AuditableEventOccurred), new Message<string, AuditEventMessage>
                    {
                        Value = auditMessage,
                        Headers = headers
                    });

                    producer.Flush();
                }


                return patientDispatch.FacilityId;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Create patient dispatch exception for patient id {patientDispatch.PatientId} in facility {patientDispatch.FacilityId}.", ex);
                throw new ApplicationException($"Failed to create patient dispatch record for patient id {patientDispatch.PatientId} in facility {patientDispatch.FacilityId}.");
            }
        }
    }
}
