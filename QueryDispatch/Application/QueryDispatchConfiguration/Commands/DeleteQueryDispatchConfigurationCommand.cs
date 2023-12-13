using Confluent.Kafka;
using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.QueryDispatch.Presentation.Services;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Quartz;

namespace LantanaGroup.Link.QueryDispatch.Application.QueryDispatchConfiguration.Commands
{
    public class DeleteQueryDispatchConfigurationCommand : IDeleteQueryDispatchConfigurationCommand
    {
        private readonly ILogger<DeleteQueryDispatchConfigurationCommand> _logger;
        private readonly IQueryDispatchConfigurationRepository _dataStore;
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly IKafkaProducerFactory<string, AuditEventMessage> _kafkaProducerFactory;

        public DeleteQueryDispatchConfigurationCommand(ILogger<DeleteQueryDispatchConfigurationCommand> logger, IQueryDispatchConfigurationRepository dataStore, ISchedulerFactory schedulerFactory, IKafkaProducerFactory<string, AuditEventMessage> kafkaProducerFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
            _schedulerFactory = schedulerFactory ?? throw new ArgumentNullException(nameof(schedulerFactory));
            _kafkaProducerFactory = kafkaProducerFactory ?? throw new ArgumentNullException(nameof(kafkaProducerFactory));
        }

        public async Task<bool> Execute(string facilityId)
        {
            try
            {
                if (string.IsNullOrEmpty(facilityId))
                {
                    throw new ArgumentNullException(nameof(facilityId));
                }

                bool result = await _dataStore.DeleteByFacilityId(facilityId);

                _logger.LogInformation($"Deleted query dispatch configuration for facility {facilityId}");
      

                using (var producer = _kafkaProducerFactory.CreateAuditEventProducer())
                {
                    var auditMessage = new AuditEventMessage
                    {
                        FacilityId = facilityId,
                        ServiceName = QueryDispatchConstants.ServiceName,
                        Action = AuditEventType.Delete,
                        EventDate = DateTime.UtcNow,
                        Resource = typeof(QueryDispatchConfigurationEntity).Name,
                        Notes = $"Deleted query dispatch configuration for facility {facilityId}"
                    };

                    producer.Produce(nameof(KafkaTopic.AuditableEventOccurred), new Message<string, AuditEventMessage>
                    {
                        Value = auditMessage
                    });

                    producer.Flush();
                }

                await ScheduleService.DeleteJob(facilityId, await _schedulerFactory.GetScheduler());

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to delete query dispatch configuration for facilityId {facilityId}", ex);
                throw new ApplicationException($"Failed to delete query dispatch configuration for facilityId {facilityId}");
            }
        }
    }
}
