using Confluent.Kafka;
using KellermanSoftware.CompareNetObjects;
using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.QueryDispatch.Presentation.Services;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Quartz;
using QueryDispatch.Application.Settings;

namespace QueryDispatch.Domain.Managers
{

    public interface IQueryDispatchConfigurationManager
    {
        public Task SaveConfigEntity(QueryDispatchConfigurationEntity config, List<DispatchSchedule> dispatchSchedules, CancellationToken cancellationToken);
        public Task AddConfigEntity(QueryDispatchConfigurationEntity config, CancellationToken cancellationToken);
        public Task DeleteConfigEntity(string facilityId, CancellationToken cancellationToken);
        public Task<QueryDispatchConfigurationEntity> GetConfigEntity(string facilityId, CancellationToken cancellationToken);
    }


    public class QueryDispatchConfigurationManager : IQueryDispatchConfigurationManager
    {
        private readonly IBaseEntityRepository<QueryDispatchConfigurationEntity> _repository;
        private readonly ILogger<QueryDispatchConfigurationManager> _logger;
        private readonly IProducer<string, AuditEventMessage> _producer;
        private readonly CompareLogic _compareLogic;
        private readonly ISchedulerFactory _schedulerFactory;

        public QueryDispatchConfigurationManager(ILogger<QueryDispatchConfigurationManager> logger, IDatabase database, ISchedulerFactory schedulerFactory, IProducer<string, AuditEventMessage> producer)
        {
            _repository = database.QueryDispatchConfigurationRepo;
            _logger = logger;
            _compareLogic = new CompareLogic();
            _compareLogic.Config.MaxDifferences = 25;
            _schedulerFactory = schedulerFactory ?? throw new ArgumentNullException(nameof(schedulerFactory));
            _producer = producer ?? throw new ArgumentNullException(nameof(producer));
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

                _logger.LogInformation($"Updated query dispatch configuration for facility {HtmlInputSanitizer.Sanitize(config.FacilityId)}");



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

                    _producer.Produce(nameof(KafkaTopic.AuditableEventOccurred), new Message<string, AuditEventMessage>
                    {
                        Value = auditMessage
                    });

                    _producer.Flush();
                
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to update query dispatch configuration for facility {HtmlInputSanitizer.Sanitize(config.FacilityId)}.", ex);
                throw new ApplicationException($"Failed to update query dispatch configuration for facility {HtmlInputSanitizer.Sanitize(config.FacilityId)}.");
            }
        }



        public async Task AddConfigEntity(QueryDispatchConfigurationEntity config, CancellationToken cancellationToken)
        {
            try
            {
                await _repository.AddAsync(config, cancellationToken);

                _logger.LogInformation($"Created query dispatch configuration for facility {HtmlInputSanitizer.Sanitize(config.FacilityId)}");


                    var auditMessage = new AuditEventMessage
                    {
                        FacilityId = config.FacilityId,
                        ServiceName = QueryDispatchConstants.ServiceName,
                        Action = AuditEventType.Create,
                        EventDate = DateTime.UtcNow,
                        Resource = typeof(QueryDispatchConfigurationEntity).Name,
                        Notes = $"Created query dispatch configuration {config.Id} for facility {config.FacilityId}"
                    };

                    _producer.Produce(nameof(KafkaTopic.AuditableEventOccurred), new Message<string, AuditEventMessage>
                    {
                        Value = auditMessage
                    });

                    _producer.Flush();
                

            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to create query dispatch configuration for facility {HtmlInputSanitizer.Sanitize(config.FacilityId)}.", ex);
                throw new ApplicationException($"Failed to create query dispatch configuration for facility {HtmlInputSanitizer.Sanitize(config.FacilityId)}.");
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

                _logger.LogInformation($"Deleted query dispatch configuration for facility {HtmlInputSanitizer.Sanitize(facilityId)}");


                    var auditMessage = new AuditEventMessage
                    {
                        FacilityId = facilityId,
                        ServiceName = QueryDispatchConstants.ServiceName,
                        Action = AuditEventType.Delete,
                        EventDate = DateTime.UtcNow,
                        Resource = typeof(QueryDispatchConfigurationEntity).Name,
                        Notes = $"Deleted query dispatch configuration for facility {facilityId}"
                    };

                    _producer.Produce(nameof(KafkaTopic.AuditableEventOccurred), new Message<string, AuditEventMessage>
                    {
                        Value = auditMessage
                    });

                    _producer.Flush();
                

                await ScheduleService.DeleteJob(facilityId, await _schedulerFactory.GetScheduler());
                
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to delete query dispatch configuration for facilityId {HtmlInputSanitizer.Sanitize(facilityId)}", ex);
                throw new ApplicationException($"Failed to delete query dispatch configuration for facilityId {HtmlInputSanitizer.Sanitize(facilityId)}");
            }
        }


        public async Task<QueryDispatchConfigurationEntity> GetConfigEntity(string facilityId, CancellationToken cancellationToken)
        {
            return await _repository.FirstOrDefaultAsync(x => x.FacilityId == facilityId);

        }
    }
}

