using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using System.Text;

namespace LantanaGroup.Link.Shared.Jobs;

[DisallowConcurrentExecution]
public class RetryJob : IJob
{
    private readonly ILogger _logger; private readonly IKafkaProducerFactory<string, string> _retryKafkaProducerFactory; private readonly ISchedulerFactory _schedulerFactory; private readonly IServiceScopeFactory _serviceScopeFactory;

    public RetryJob(
        ILogger<RetryJob> logger,
        IKafkaProducerFactory<string, string> retryKafkaProducerFactory,
        ISchedulerFactory schedulerFactory,
        IServiceScopeFactory serviceScopeFactory)
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
            var _retryRepository = scope.ServiceProvider.GetRequiredService<IBaseEntityRepository<RetryModel>>();

            var triggerMap = context.Trigger.JobDataMap;
            var retryModel = (RetryModel)triggerMap["RetryModel"];

            _logger.LogInformation("Executing RetryJob for {Topic}-{Id}", retryModel.Topic, retryModel.Id);

            // remove the job from the scheduler and database
            await RetryScheduleService.DeleteJob(retryModel, await _schedulerFactory.GetScheduler());
            await _retryRepository.DeleteAsync(retryModel.Id);

            ProducerConfig config = new ProducerConfig()
            {
                CompressionType = CompressionType.Zstd
            };

            Headers headers = new Headers();

            foreach (var header in retryModel.Headers)
            {
                headers.Add(header.Key, Encoding.UTF8.GetBytes(header.Value));
            }

            //Remove the Retry Count Header and replace it with a new value.
            if (headers.Any(h => h.Key == KafkaConstants.HeaderConstants.RetryCount))
            {
                headers.Remove(KafkaConstants.HeaderConstants.RetryCount);
            }

            headers.Add(KafkaConstants.HeaderConstants.RetryCount, Encoding.UTF8.GetBytes(retryModel.RetryCount.ToString()));

            using (var producer = _retryKafkaProducerFactory.CreateProducer(config, useOpenTelemetry: false))
            {

                var darKey = retryModel.Key;
                var darValue = retryModel.Value;

                producer.Produce(retryModel.Topic,
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
            _logger.LogError(ex, "Error encountered in GenerateDataAcquisitionRequestsForPatientsToQuery: {Message}\n{StackTrace}", ex.Message, ex.StackTrace);
            throw;
        }
    }
}