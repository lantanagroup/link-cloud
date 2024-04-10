using Confluent.Kafka;
using KellermanSoftware.CompareNetObjects;
using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using QueryDispatch.Application.Settings;

namespace LantanaGroup.Link.QueryDispatch.Application.QueryDispatchConfiguration.Commands
{
    public class UpdateQueryDispatchConfigurationCommand : IUpdateQueryDispatchConfigurationCommand
    {
        private readonly ILogger<UpdateQueryDispatchConfigurationCommand> _logger;
        private readonly IQueryDispatchConfigurationRepository _dataStore;
        private readonly IKafkaProducerFactory<string, AuditEventMessage> _kafkaProducerFactory;
        private readonly CompareLogic _compareLogic;

        public UpdateQueryDispatchConfigurationCommand(ILogger<UpdateQueryDispatchConfigurationCommand> logger, IQueryDispatchConfigurationRepository dataStore, IKafkaProducerFactory<string, AuditEventMessage> kafkaProducerFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
            _kafkaProducerFactory = kafkaProducerFactory ?? throw new ArgumentNullException(nameof(kafkaProducerFactory));
            _compareLogic = new CompareLogic();
            _compareLogic.Config.MaxDifferences = 25;
        }

        public async Task Execute(QueryDispatchConfigurationEntity config, List<DispatchSchedule> dispatchSchedules)
        {
            try
            {
                var resultChanges = _compareLogic.Compare(config.DispatchSchedules, dispatchSchedules);
                List<Difference> list = resultChanges.Differences;
                List<PropertyChangeModel> propertyChanges = new List<PropertyChangeModel>();
                list.ForEach(d => {
                    propertyChanges.Add(new PropertyChangeModel
                    {
                        PropertyName = d.PropertyName,
                        InitialPropertyValue = d.Object1Value,
                        NewPropertyValue = d.Object2Value
                    });

                });

                config.DispatchSchedules = dispatchSchedules;
                config.ModifyDate = DateTime.UtcNow;

                await _dataStore.Update(config);

                _logger.LogInformation($"Updated query dispatch configuration for facility {config.FacilityId}");

                using (var producer = _kafkaProducerFactory.CreateAuditEventProducer())
                {

                    var auditMessage = new AuditEventMessage
                    {
                        FacilityId = config.FacilityId,
                        ServiceName = QueryDispatchConstants.ServiceName,
                        Action = AuditEventType.Update,
                        EventDate = DateTime.UtcNow,
                        PropertyChanges = propertyChanges,
                        Resource = typeof(QueryDispatchConfigurationEntity).Name,
                        Notes = $"Updated query dispatch configuration {config.Id} for facility {config.FacilityId}"
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
                _logger.LogError($"Failed to update query dispatch configuration for facility {config.FacilityId}.", ex);
                throw new ApplicationException($"Failed to update query dispatch configuration for facility {config.FacilityId}.");
            }
        }
    }
}
