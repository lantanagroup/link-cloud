using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.Extensions.DependencyInjection;
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
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public RetryJob(ILogger<RetryJob> logger,
            IKafkaProducerFactory<string, string> retryKafkaProducerFactory,
            ISchedulerFactory schedulerFactory,
            IServiceScopeFactory serviceScopeFactory
            )
        {
            _logger = logger;
            _retryKafkaProducerFactory = retryKafkaProducerFactory;
            _schedulerFactory = schedulerFactory;
            _serviceScopeFactory = serviceScopeFactory;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var _retryRepository = scope.ServiceProvider.GetRequiredService<IBaseEntityRepository<RetryEntity>>();

                var triggerMap = context.Trigger.JobDataMap;
                var retryEntity = (RetryEntity)triggerMap["RetryEntity"];

                _logger.LogInformation($"Executing RetryJob for {retryEntity.Topic}-{retryEntity.Id}");

                // remove the job from the scheduler and database
                await RetryScheduleService.DeleteJob(retryEntity, await _schedulerFactory.GetScheduler());
                await _retryRepository.DeleteAsync(retryEntity.Id);

                ProducerConfig config = new ProducerConfig()
                { 
                    CompressionType = CompressionType.Zstd
                };

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
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error encountered in GenerateDataAcquisitionRequestsForPatientsToQuery: {ex.Message + Environment.NewLine + ex.StackTrace}");
                throw;
            }
        }
    }
}
