using Confluent.Kafka;
using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.QueryDispatch.Application.QueryDispatchConfiguration.Commands
{
    public class CreateQueryDispatchConfigurationCommand : ICreateQueryDispatchConfigurationCommand
    {
        private readonly ILogger<CreateQueryDispatchConfigurationCommand> _logger;
        private readonly IQueryDispatchConfigurationRepository _dataStore;
        private readonly IKafkaProducerFactory<string, AuditEventMessage> _kafkaProducerFactory;


        public CreateQueryDispatchConfigurationCommand(ILogger<CreateQueryDispatchConfigurationCommand> logger, IQueryDispatchConfigurationRepository dataStore, IKafkaProducerFactory<string, AuditEventMessage> kafkaProducerFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
            _kafkaProducerFactory = kafkaProducerFactory ?? throw new ArgumentNullException(nameof(kafkaProducerFactory));
        }

        public async Task Execute(QueryDispatchConfigurationEntity config)
        {
            try
            {
                await _dataStore.AddAsync(config);

                _logger.LogInformation($"Created query dispatch configuration for facility {config.FacilityId}");

                using(var producer = _kafkaProducerFactory.CreateAuditEventProducer())
                {

                    var auditMessage = new AuditEventMessage
                    {
                        FacilityId = config.FacilityId,
                        ServiceName = QueryDispatchConstants.ServiceName,
                        Action = AuditEventType.Create,
                        EventDate = DateTime.UtcNow,
                        Resource = typeof(QueryDispatchConfigurationEntity).Name,
                        Notes = $"Created query dispatch configuration {config.Id} for facility {config.FacilityId}"
                    };

                    producer.Produce(nameof(KafkaTopic.AuditableEventOccurred), new Message<string, AuditEventMessage>
                    {
                        Value = auditMessage
                    });

                    producer.Flush();
                }

            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to create query dispatch configuration for facility {config.FacilityId}.", ex);
                throw new ApplicationException($"Failed to create query dispatch configuration for facility {config.FacilityId}.");
            }
        }
    }
}
