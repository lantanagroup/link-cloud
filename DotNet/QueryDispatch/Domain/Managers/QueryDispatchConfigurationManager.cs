using Confluent.Kafka;
using KellermanSoftware.CompareNetObjects;
using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.QueryDispatch.Presentation.Services;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Quartz;
using QueryDispatch.Application.Settings;

namespace QueryDispatch.Domain.Managers
{
    public class QueryDispatchConfigurationManager
    {

        private readonly IQueryDispatchConfigurationRepository _repository;
        private readonly ILogger<QueryDispatchConfigurationManager> _logger;
        private readonly IKafkaProducerFactory<string, AuditEventMessage> _kafkaProducerFactory;
        private readonly CompareLogic _compareLogic;
        private readonly ISchedulerFactory _schedulerFactory;

        public QueryDispatchConfigurationManager(ILogger<QueryDispatchConfigurationManager> logger, IQueryDispatchConfigurationRepository queryDispatchConfigRepo, IKafkaProducerFactory<string, AuditEventMessage> kafkaProducerFactory, ISchedulerFactory schedulerFactory)
        {
            _repository = queryDispatchConfigRepo;
            _kafkaProducerFactory = kafkaProducerFactory ?? throw new ArgumentNullException(nameof(kafkaProducerFactory));
            _logger = logger;
            _compareLogic = new CompareLogic();
            _compareLogic.Config.MaxDifferences = 25;
            _schedulerFactory = schedulerFactory ?? throw new ArgumentNullException(nameof(schedulerFactory));
        }

        public async Task SaveConfigEntity(QueryDispatchConfigurationEntity config, List<DispatchSchedule> dispatchSchedules, CancellationToken cancellationToken)
        {
            try
            {
                var resultChanges = _compareLogic.Compare(config.DispatchSchedules, dispatchSchedules);
                List<Difference> list = resultChanges.Differences;
                List<PropertyChangeModel> propertyChanges = new List<PropertyChangeModel>();
                list.ForEach(d =>
                {
                    propertyChanges.Add(new PropertyChangeModel
                    {
                        PropertyName = d.PropertyName,
                        InitialPropertyValue = d.Object1Value,
                        NewPropertyValue = d.Object2Value
                    });

                });

                config.DispatchSchedules = dispatchSchedules;
                config.ModifyDate = DateTime.UtcNow;

                await _repository.UpdateAsync(config, cancellationToken);

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



        public async Task AddConfigEntity(QueryDispatchConfigurationEntity config, CancellationToken cancellationToken)
        {
            try
            {
                await _repository.AddAsync(config, cancellationToken);

                _logger.LogInformation($"Created query dispatch configuration for facility {config.FacilityId}");

                using (var producer = _kafkaProducerFactory.CreateAuditEventProducer())
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

        public async Task DeleteConfigEntity(string facilityId, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrEmpty(facilityId))
                {
                    throw new ArgumentNullException(nameof(facilityId));
                }

                var config = await _repository.FirstOrDefaultAsync(x => x.FacilityId == facilityId);

                if(config == null)
                {
                    return;
                }
                await _repository.DeleteAsync(config.Id, cancellationToken);

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
                
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to delete query dispatch configuration for facilityId {facilityId}", ex);
                throw new ApplicationException($"Failed to delete query dispatch configuration for facilityId {facilityId}");
            }
        }

    }
}

