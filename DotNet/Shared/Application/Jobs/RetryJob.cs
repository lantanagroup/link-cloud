using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using LantanaGroup.Link.Shared.Application.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using Quartz;
using System.Text;

namespace LantanaGroup.Link.Shared.Jobs
{
    [DisallowConcurrentExecution]
    public class RetryJob : IJob
    {
        private readonly ILogger<RetryJob> _logger;
        private readonly IKafkaProducerFactory<string, string> _retryKafkaProducerFactory;
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly RetryRepository_Mongo _retryRepository;

        public RetryJob(ILogger<RetryJob> logger,
            IKafkaProducerFactory<string, string> retryKafkaProducerFactory,
            ISchedulerFactory schedulerFactory,
            RetryRepository_Mongo retryRepository)
        {
            _logger = logger;
            _retryKafkaProducerFactory = retryKafkaProducerFactory;
            _schedulerFactory = schedulerFactory;
            _retryRepository = retryRepository;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                var triggerMap = context.Trigger.JobDataMap;
                var retryEntity = (RetryEntity)triggerMap["RetryEntity"];

                _logger.LogInformation($"Executing RetryJob for {retryEntity.Topic}-{retryEntity.Id}");

                // remove the job from the scheduler and database
                await RetryScheduleService.DeleteJob(retryEntity, await _schedulerFactory.GetScheduler());
                await _retryRepository.DeleteAsync(retryEntity.Id);

                ProducerConfig config = new ProducerConfig();
                Headers headers = new Headers();

                foreach (var header in retryEntity.Headers)
                {
                    headers.Add(header.Key, Encoding.UTF8.GetBytes(header.Value));
                }


                using (var producer = _retryKafkaProducerFactory.CreateProducer(config, useOpenTelemetry: false))
                {

                    var darKey = retryEntity.Key;
                    var darValue = retryEntity.Value;

                    producer.Produce(retryEntity.Topic,
                        new Message<string, string>
                        {
                            Key = darKey,
                            Value = darValue,
                            Headers = headers
                        });

                    producer.Flush();
                }

                using (var producer = _retryKafkaProducerFactory.CreateAuditEventProducer(useOpenTelemetry: false))
                {
                    try
                    {
                        var val = new AuditEventMessage
                        {
                            FacilityId = retryEntity.FacilityId,
                            ServiceName = retryEntity.ServiceName,
                            Action = AuditEventType.Create,
                            EventDate = DateTime.UtcNow,
                            Resource = retryEntity.Topic,
                            Notes = retryEntity.JobId
                        };

                        producer.Produce(nameof(KafkaTopic.AuditableEventOccurred),
                            new Message<string, AuditEventMessage>
                            {
                                Value = val,
                                Headers = headers
                            });
                        producer.Flush();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to generate a {nameof(KafkaTopic.AuditableEventOccurred)} message");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error encountered in GenerateDataAcquisitionRequestsForPatientsToQuery: {ex.Message + Environment.NewLine + ex.StackTrace}");
                throw;
            }
        }
    }
}
